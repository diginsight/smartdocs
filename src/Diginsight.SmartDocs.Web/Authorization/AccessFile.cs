using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>What reading a space's <c>access.metadata.yml</c> produced.</summary>
public enum AccessFileStatus
{
    /// <summary>No file at the space's content root.</summary>
    Missing,

    /// <summary>A file whose content passed every rule.</summary>
    Valid,

    /// <summary>A file that was read but broke a rule. The space is hidden until a valid file is published.</summary>
    Invalid,

    /// <summary>A read that failed for a reason other than "not found". Never cached.</summary>
    Unreadable,
}

/// <summary>
/// A space's parsed <c>access.metadata.yml</c>: the permissions it declares, the ones a reader needs
/// to see the space, and what each app role grants in this space only.
/// <para>
/// The parser is strict on purpose. An unknown key, a duplicate or reserved name, an unknown schema,
/// or a reference to an undeclared permission makes the whole file invalid, and an invalid file hides
/// its space, so a typo can never publish what it was meant to restrict.
/// </para>
/// </summary>
public sealed partial class AccessFile
{
    public const string FileName = "access.metadata.yml";

    public const int SupportedSchema = 1;

    /// <summary>
    /// Upper bound on the file's size. An access file is a few hundred bytes; the bound keeps a
    /// mistaken or hostile upload — YAML aliases included — from costing more than a page to parse.
    /// </summary>
    public const int MaxBytes = 64 * 1024;

    private const int MaxQuotedLength = 80;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithDuplicateKeyChecking()
        .Build();

    private static readonly ImmutableSortedSet<string> EmptySet =
        ImmutableSortedSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    private AccessFile(
        ImmutableSortedSet<string> permissions,
        ImmutableSortedSet<string> requires,
        ImmutableSortedDictionary<string, ImmutableSortedSet<string>> roles,
        string fingerprint)
    {
        Permissions = permissions;
        Requires = requires;
        Roles = roles;
        Fingerprint = fingerprint;
    }

    /// <summary>The permission names the space declares, case-insensitive.</summary>
    public ImmutableSortedSet<string> Permissions { get; }

    /// <summary>The permissions a reader needs to see the space, all of them. May contain <c>authenticated</c>.</summary>
    public ImmutableSortedSet<string> Requires { get; }

    /// <summary>What each app role value grants in this space. Keys and values are case-insensitive.</summary>
    public ImmutableSortedDictionary<string, ImmutableSortedSet<string>> Roles { get; }

    /// <summary>Lower-case hexadecimal SHA-256 of the file's bytes.</summary>
    public string Fingerprint { get; }

    /// <summary>What <paramref name="role"/> grants in this space, or nothing when the space doesn't map it.</summary>
    public ImmutableSortedSet<string> Grants(string role) =>
        Roles.TryGetValue(role, out ImmutableSortedSet<string>? granted) ? granted : EmptySet;

    /// <summary>
    /// Parses the bytes of an access file. Never throws for bad content: anything the parser or the
    /// rules reject answers <see cref="AccessFileStatus.Invalid"/>.
    /// </summary>
    public static AccessFileOutcome Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (bytes.Length > MaxBytes)
        {
            return AccessFileOutcome.Invalid(
                $"The file is {bytes.Length} bytes long; an access file may be at most {MaxBytes} bytes.", fingerprint);
        }

        Document? document;
        try
        {
            using var reader = new StreamReader(
                new MemoryStream(bytes, writable: false),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            var parser = new Parser(reader);
            parser.Consume<StreamStart>();
            document = Deserializer.Deserialize<Document>(parser);
            if (!parser.Accept<StreamEnd>(out _))
            {
                return AccessFileOutcome.Invalid(
                    "The file holds more than one YAML document; an access file is a single document.", fingerprint);
            }
        }
        catch (YamlException ex)
        {
            string detail = Innermost(ex).Message;
            detail = UnknownKeyPattern().Match(detail) is { Success: true } unknown
                ? $"{Quote(unknown.Groups["key"].Value)} isn't a key an access file accepts; the keys are 'schema', 'permissions', 'requires', and 'roles'."
                : Sanitize(detail);
            return AccessFileOutcome.Invalid(
                $"The YAML is invalid at line {ex.Start.Line}, column {ex.Start.Column}: {detail}", fingerprint);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Untrusted content: whatever else the reader or the deserializer throws is a fault of
            // the file, so it closes the space as invalid rather than escaping as a read failure.
            return AccessFileOutcome.Invalid($"The file can't be read as YAML: {Sanitize(Innermost(ex).Message)}", fingerprint);
        }

        if (document is null)
        {
            return AccessFileOutcome.Invalid($"The file is empty. It must contain at least 'schema: {SupportedSchema}'.", fingerprint);
        }

        return Validate(document, fingerprint);
    }

    /// <summary>
    /// True when <paramref name="key"/> names an access file at any depth. The key is canonicalized
    /// first — dot segments resolved, trailing dots and spaces removed, alternate data streams cut —
    /// because a file system can resolve <c>access.metadata.yml.</c> or <c>access.metadata.yml::$DATA</c>
    /// to the same file, and the exclusion must hold for every spelling that reaches it.
    /// </summary>
    public static bool IsAccessFileKey(string? key) =>
        CanonicalSegments(key) is { Count: > 0 } segments
        && string.Equals(segments[^1], FileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when <paramref name="innerKey"/>, a key inside one space, names an access file below the
    /// space's content root. Version 1 reads only the root file, so a deeper one restricts nothing.
    /// </summary>
    public static bool IsNestedAccessFileKey(string? innerKey) =>
        CanonicalSegments(innerKey) is { Count: > 1 } segments
        && string.Equals(segments[^1], FileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Rebuilds a file that <see cref="Parse"/> already validated, from a cached copy.</summary>
    internal static AccessFile Restore(
        IEnumerable<string> permissions,
        IEnumerable<string> requires,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> roles,
        string fingerprint) =>
        new(
            ToSet(permissions),
            ToSet(requires),
            roles.ToImmutableSortedDictionary(
                static pair => pair.Key,
                static pair => ToSet(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            fingerprint);

    private static ImmutableSortedSet<string> ToSet(IEnumerable<string> names) =>
        ImmutableSortedSet.CreateRange(StringComparer.OrdinalIgnoreCase, names);

    private static AccessFileOutcome Validate(Document document, string fingerprint)
    {
        if (document.Schema is not { } schema)
        {
            return AccessFileOutcome.Invalid($"'schema' is missing. Add 'schema: {SupportedSchema}'.", fingerprint);
        }

        if (schema != SupportedSchema)
        {
            return AccessFileOutcome.Invalid(
                $"'schema' is {schema}, but the only supported version is {SupportedSchema}.", fingerprint);
        }

        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in document.Permissions?.Keys ?? Enumerable.Empty<string>())
        {
            if (NameProblem(name, "permission name") is { } problem)
            {
                return AccessFileOutcome.Invalid(problem, fingerprint);
            }

            if (IsAuthenticated(name))
            {
                return AccessFileOutcome.Invalid(
                    $"'{AccessRequirement.Authenticated}' is reserved for every signed-in reader, so 'permissions' can't declare it.",
                    fingerprint);
            }

            if (!declared.Add(name))
            {
                return AccessFileOutcome.Invalid($"Permission '{name}' is declared more than once, ignoring case.", fingerprint);
            }
        }

        var requires = new List<string>();
        foreach (string? entry in document.Requires ?? [])
        {
            if (NameProblem(entry, "permission name in 'requires'") is { } problem)
            {
                return AccessFileOutcome.Invalid(problem, fingerprint);
            }

            string name = entry!;
            if (IsAuthenticated(name))
            {
                requires.Add(AccessRequirement.Authenticated);
                continue;
            }

            if (!declared.Contains(name))
            {
                return AccessFileOutcome.Invalid($"'requires' names '{name}', which 'permissions' doesn't declare.", fingerprint);
            }

            requires.Add(name);
        }

        var roleValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roles = new List<KeyValuePair<string, IEnumerable<string>>>();
        foreach ((string role, List<string?>? granted) in
                 document.Roles ?? Enumerable.Empty<KeyValuePair<string, List<string?>?>>())
        {
            if (NameProblem(role, "role value") is { } problem)
            {
                return AccessFileOutcome.Invalid(problem, fingerprint);
            }

            if (!roleValues.Add(role))
            {
                return AccessFileOutcome.Invalid($"Role '{role}' is mapped more than once, ignoring case.", fingerprint);
            }

            var permissions = new List<string>();
            foreach (string? entry in granted ?? [])
            {
                if (NameProblem(entry, $"permission name granted by role '{role}'") is { } grantProblem)
                {
                    return AccessFileOutcome.Invalid(grantProblem, fingerprint);
                }

                string name = entry!;
                if (IsAuthenticated(name))
                {
                    return AccessFileOutcome.Invalid(
                        $"Role '{role}' grants '{AccessRequirement.Authenticated}', which is reserved for every signed-in reader.",
                        fingerprint);
                }

                if (!declared.Contains(name))
                {
                    return AccessFileOutcome.Invalid(
                        $"Role '{role}' grants '{name}', which 'permissions' doesn't declare.", fingerprint);
                }

                permissions.Add(name);
            }

            roles.Add(new(role, permissions));
        }

        return AccessFileOutcome.Valid(Restore(declared, requires, roles, fingerprint));
    }

    private static bool IsAuthenticated(string name) =>
        string.Equals(name, AccessRequirement.Authenticated, StringComparison.OrdinalIgnoreCase);

    /// <summary>Why <paramref name="name"/> isn't a valid name, or null. Only valid names are echoed unquoted elsewhere.</summary>
    private static string? NameProblem(string? name, string what) =>
        name is not null && NamePattern().IsMatch(name)
            ? null
            : $"{Quote(name)} isn't a valid {what}: a name starts with a letter and continues with letters, digits, '.', '-', or '_'.";

    /// <summary>Quotes untrusted text for a log line: control characters escaped, length bounded.</summary>
    private static string Quote(string? text) =>
        text is null ? "An empty entry" : "'" + Sanitize(text) + "'";

    private static string Sanitize(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, MaxQuotedLength) + 8);
        foreach (char c in text)
        {
            if (builder.Length >= MaxQuotedLength)
            {
                builder.Append('…');
                break;
            }

            if (char.IsControl(c))
            {
                builder.Append("\\u").Append(((int)c).ToString("x4"));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static Exception Innermost(Exception ex)
    {
        while (ex.InnerException is not null)
        {
            ex = ex.InnerException;
        }

        return ex;
    }

    private static List<string> CanonicalSegments(string? key)
    {
        var segments = new List<string>();
        foreach (string raw in (key ?? string.Empty).Replace('\\', '/').Split('/'))
        {
            if (raw.Length == 0 || raw == ".")
            {
                continue;
            }

            if (raw == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            string segment = raw;
            int colon = segment.IndexOf(':');
            if (colon >= 0)
            {
                segment = segment[..colon];
            }

            segment = segment.TrimEnd('.', ' ');
            if (segment.Length > 0)
            {
                segments.Add(segment);
            }
        }

        return segments;
    }

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9._-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"\AProperty '(?<key>[^']*)' not found on type", RegexOptions.CultureInvariant)]
    private static partial Regex UnknownKeyPattern();

    /// <summary>The file's shape. Unknown keys fail deserialization, because unmatched properties aren't ignored.</summary>
    private sealed class Document
    {
        [YamlMember(Alias = "schema")]
        public int? Schema { get; set; }

        [YamlMember(Alias = "permissions")]
        public Dictionary<string, string?>? Permissions { get; set; }

        [YamlMember(Alias = "requires")]
        public List<string?>? Requires { get; set; }

        [YamlMember(Alias = "roles")]
        public Dictionary<string, List<string?>?>? Roles { get; set; }
    }
}

/// <summary>
/// The result of reading one space's access file: <see cref="AccessFileStatus.Valid"/> carries the
/// parsed <see cref="File"/>; <see cref="AccessFileStatus.Invalid"/> and
/// <see cref="AccessFileStatus.Unreadable"/> carry the <see cref="Problem"/> in words an author can act on.
/// </summary>
public sealed class AccessFileOutcome
{
    private AccessFileOutcome(AccessFileStatus status, AccessFile? file, string? problem, string? fingerprint)
    {
        Status = status;
        File = file;
        Problem = problem;
        Fingerprint = fingerprint;
    }

    public static AccessFileOutcome Missing { get; } = new(AccessFileStatus.Missing, null, null, null);

    public AccessFileStatus Status { get; }

    public AccessFile? File { get; }

    public string? Problem { get; }

    /// <summary>SHA-256 of the bytes that were read, when a file was read.</summary>
    public string? Fingerprint { get; }

    public static AccessFileOutcome Valid(AccessFile file) =>
        new(AccessFileStatus.Valid, file, null, file.Fingerprint);

    public static AccessFileOutcome Invalid(string problem, string? fingerprint) =>
        new(AccessFileStatus.Invalid, null, problem, fingerprint);

    public static AccessFileOutcome Unreadable(string problem) =>
        new(AccessFileStatus.Unreadable, null, problem, null);
}
