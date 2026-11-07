using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Diginsight.SmartDocs.Web.Metadata;

/// <summary>Parses one complete metadata file and validates its scope and access policy.</summary>
public sealed class MetadataParser : IMetadataDocumentParser
{
    public const string FileName = "metadata.yml";
    public const string Schema = "smartdocs-metadata/v1";
    public const int MaxBytes = AccessFile.MaxBytes;

    private static readonly IReadOnlyDictionary<string, string> NavigationKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = "label", ["nav-label"] = "label",
            ["short"] = "short", ["nav-short"] = "short",
            ["icon"] = "icon", ["nav-icon"] = "icon",
            ["order"] = "order", ["nav-order"] = "order",
            ["hidden"] = "hidden", ["nav-hidden"] = "hidden",
            ["topbar-hidden"] = "topbar-hidden", ["nav-topbar-hidden"] = "topbar-hidden", ["hidden-topbar"] = "topbar-hidden",
            ["topbar-align"] = "topbar-align", ["nav-topbar-align"] = "topbar-align",
            ["article-count"] = "article-count", ["nav-article-count"] = "article-count", ["articles"] = "article-count",
            ["latest-article"] = "latest-article", ["nav-latest-article"] = "latest-article", ["updated"] = "latest-article",
        };

    private static readonly HashSet<string> RepositoryKeys = new(StringComparer.Ordinal)
    {
        "id", "visibility", "internal_peer", "deliberately_public",
    };

    private static readonly HashSet<string> PeerKeys = new(StringComparer.Ordinal)
    {
        "id", "path_env", "default_relative_path", "mirror_strategy", "suffix", "on_missing", "alias_registry",
    };

    private static readonly HashSet<string> AccessKeys = new(StringComparer.Ordinal)
    {
        "permissions", "requires", "roles",
    };

    private static readonly HashSet<string> ReservedNavigationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "schema", "access", "repository", "permissions", "requires", "roles",
        "id", "visibility", "internal_peer", "deliberately_public",
    };

    public static bool IsReservedNavigationKey(string key) => ReservedNavigationKeys.Contains(key);

    public AccessFileOutcome ParseLegacyAccess(byte[] bytes) => AccessFile.Parse(bytes);

    public MetadataOutcome Parse(byte[] bytes, bool isContentRoot)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (bytes.Length > MaxBytes)
        {
            return MetadataOutcome.Invalid($"Metadata exceeds the {MaxBytes}-byte limit.", fingerprint);
        }

        try
        {
            string text = new UTF8Encoding(false, true).GetString(bytes);
            var yaml = new YamlStream();
            string normalized = NormalizeLegacyFlatDocument(text, out bool legacyFlat);
            yaml.Load(new StringReader(normalized));
            if (yaml.Documents.Count == 0 ||
                (yaml.Documents.Count == 1 && IsNull(yaml.Documents[0].RootNode)))
            {
                return MetadataOutcome.Valid(new MetadataDocument(
                    false, FolderMeta.None, null, null, fingerprint, []));
            }

            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode rootNode)
            {
                throw new MetadataFormatException("Metadata must contain one YAML mapping document.");
            }

            Dictionary<string, YamlNode> root = Mapping(rootNode, "metadata", StringComparer.OrdinalIgnoreCase);
            bool versioned = root.TryGetValue("schema", out YamlNode? schemaNode);
            if (versioned && Scalar(schemaNode!, "schema") != Schema)
            {
                throw new MetadataFormatException($"The supported metadata schema is '{Schema}'.");
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var warnings = ImmutableArray.CreateBuilder<string>();
            foreach ((string key, YamlNode node) in root)
            {
                if (versioned && !key.Equals(key.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    throw new MetadataFormatException("Versioned metadata keys must use their documented lowercase spelling.");
                }

                if (key.Equals("schema", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (key.Equals("access", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("repository", StringComparison.OrdinalIgnoreCase))
                {
                    if (!versioned)
                    {
                        if (node is not YamlScalarNode)
                        {
                            throw new MetadataFormatException($"'{key}' requires the unified schema.");
                        }

                        warnings.Add($"Unversioned '{key}' has no policy or repository effect.");
                    }

                    continue;
                }

                if (!versioned && IsReservedNavigationKey(key))
                {
                    if (node is not YamlScalarNode)
                    {
                        throw new MetadataFormatException($"'{key}' requires the unified schema.");
                    }

                    warnings.Add($"Unversioned '{key}' has no policy or repository effect.");
                    continue;
                }

                if (!NavigationKeys.TryGetValue(key, out string? canonical))
                {
                    if (versioned)
                    {
                        throw new MetadataFormatException($"Unknown metadata key '{Safe(key)}'.");
                    }

                    if (node is not YamlScalarNode)
                    {
                        throw new MetadataFormatException($"Legacy custom key '{Safe(key)}' must be scalar.");
                    }
                }
                else if (versioned && !aliases.Add(canonical))
                {
                    throw new MetadataFormatException($"Multiple aliases specify '{canonical}'.");
                }

                string? value = Scalar(node, key);
                if (versioned && canonical is not null && value is not null)
                {
                    ValidateNavigation(canonical, value);
                }

                values[key.ToLowerInvariant()] = value ?? string.Empty;
            }

            RepositoryMetadata? repository = null;
            AccessFile? access = null;
            if (versioned && root.TryGetValue("repository", out YamlNode? repositoryNode))
            {
                if (!isContentRoot)
                {
                    throw new MetadataFormatException("'repository' is supported only at the root.");
                }

                repository = ParseRepository(repositoryNode);
            }

            if (versioned && root.TryGetValue("access", out YamlNode? accessNode))
            {
                if (!isContentRoot)
                {
                    throw new MetadataFormatException("'access' is supported only at a space's content root.");
                }

                access = ParseAccess(accessNode, fingerprint);
            }

            FolderMeta navigation;
            if (legacyFlat && !versioned)
            {
                FolderMeta legacyNavigation = FolderMeta.Parse(text);
                navigation = legacyNavigation with
                {
                    Values = legacyNavigation.Values?
                        .Where(static pair => !IsReservedNavigationKey(pair.Key))
                        .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
                };
            }
            else
            {
                navigation = FolderMeta.FromValues(values) with
                {
                    Values = values.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
                };
            }

            return MetadataOutcome.Valid(new MetadataDocument(
                versioned, navigation, repository, access, fingerprint, warnings.ToImmutable()));
        }
        catch (MetadataFormatException ex)
        {
            return MetadataOutcome.Invalid(ex.Message, fingerprint);
        }
        catch (YamlException ex)
        {
            return MetadataOutcome.Invalid(
                $"Invalid YAML at line {ex.Start.Line}, column {ex.Start.Column}: {Safe(ex.Message)}", fingerprint);
        }
        catch (DecoderFallbackException)
        {
            return MetadataOutcome.Invalid("Metadata must be valid UTF-8.", fingerprint);
        }
        catch (ArgumentException ex)
        {
            return MetadataOutcome.Invalid($"Invalid YAML mapping: {Safe(ex.Message)}", fingerprint);
        }
    }

    private static string NormalizeLegacyFlatDocument(string text, out bool legacyFlat)
    {
        legacyFlat = false;
        string[] lines = text.Split('\n');
        var last = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? indentation = null;
        int trailingFence = -1;
        bool documentEnded = false;
        bool changed = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (IsDocumentMarker(trimmed, "---"))
            {
                if (documentEnded || trailingFence >= 0)
                {
                    return text;
                }

                if (last.Count > 0)
                {
                    trailingFence = i;
                }

                continue;
            }

            if (documentEnded || trailingFence >= 0)
            {
                return text;
            }

            if (IsDocumentMarker(trimmed, "..."))
            {
                documentEnded = true;
                continue;
            }

            int indent = line.Length - line.TrimStart(' ').Length;
            if (indent < line.Length && char.IsWhiteSpace(line[indent]))
            {
                return text;
            }

            string currentIndentation = line[..indent];
            indentation ??= currentIndentation;
            int colon = line.IndexOf(':', indent);
            if (currentIndentation != indentation || colon <= indent)
            {
                return text;
            }

            string key = line[indent..colon].TrimEnd();
            if (key.Length == 0 || key.Any(static c =>
                    !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_') ||
                key.Equals("schema", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            string value = line[(colon + 1)..].TrimStart();
            if (value.Length > 0 && value[0] is '[' or '{' or '|' or '>' or '!' or '&' or '*')
            {
                return text;
            }

            if (last.TryGetValue(key, out int previous))
            {
                lines[previous] = string.Empty;
                changed = true;
            }

            last[key] = i;
        }

        // Legacy folder metadata allowed a closing --- fence, which YAML treats as a new document.
        if (trailingFence >= 0 && last.Count > 0)
        {
            lines[trailingFence] = string.Empty;
            changed = true;
        }

        legacyFlat = true;
        return changed ? string.Join('\n', lines) : text;
    }

    private static bool IsDocumentMarker(string line, string marker) =>
        line == marker ||
        (line.StartsWith(marker, StringComparison.Ordinal) &&
         line.Length > marker.Length &&
         char.IsWhiteSpace(line[marker.Length]) &&
         line[marker.Length..].TrimStart().StartsWith('#'));

    private static RepositoryMetadata ParseRepository(YamlNode node)
    {
        Dictionary<string, YamlNode> fields = Mapping(node, "repository", StringComparer.Ordinal);
        RejectUnknown(fields, RepositoryKeys, "repository");
        var peer = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        if (fields.TryGetValue("internal_peer", out YamlNode? peerNode))
        {
            Dictionary<string, YamlNode> peerFields = Mapping(peerNode, "repository.internal_peer", StringComparer.Ordinal);
            RejectUnknown(peerFields, PeerKeys, "repository.internal_peer");
            foreach ((string key, YamlNode value) in peerFields)
            {
                peer[key] = Scalar(value, $"repository.internal_peer.{key}");
            }
        }

        ImmutableArray<string> publicValues = fields.TryGetValue("deliberately_public", out YamlNode? publicNode)
            ? Sequence(publicNode, "repository.deliberately_public").Select((item, index) =>
                Scalar(item, $"repository.deliberately_public[{index}]")
                ?? throw new MetadataFormatException("'repository.deliberately_public' entries must be strings.")).ToImmutableArray()
            : [];
        return new RepositoryMetadata(
            OptionalScalar(fields, "id"),
            OptionalScalar(fields, "visibility"),
            peer.ToImmutable(),
            publicValues);
    }

    private static AccessFile ParseAccess(YamlNode node, string fingerprint)
    {
        Dictionary<string, YamlNode> fields = Mapping(node, "access", StringComparer.Ordinal);
        RejectUnknown(fields, AccessKeys, "access");

        Dictionary<string, string?>? permissions = null;
        if (fields.TryGetValue("permissions", out YamlNode? permissionsNode) && !IsNull(permissionsNode))
        {
            permissions = Mapping(permissionsNode, "access.permissions", StringComparer.Ordinal)
                .ToDictionary(static pair => pair.Key, pair => Scalar(pair.Value, $"access.permissions.{pair.Key}"), StringComparer.Ordinal);
        }

        List<string?>? requires = null;
        if (fields.TryGetValue("requires", out YamlNode? requiresNode) && !IsNull(requiresNode))
        {
            requires = Sequence(requiresNode, "access.requires")
                .Select((item, index) => Scalar(item, $"access.requires[{index}]")).ToList();
        }

        Dictionary<string, List<string?>?>? roles = null;
        if (fields.TryGetValue("roles", out YamlNode? rolesNode) && !IsNull(rolesNode))
        {
            roles = new Dictionary<string, List<string?>?>(StringComparer.Ordinal);
            foreach ((string role, YamlNode grants) in Mapping(rolesNode, "access.roles", StringComparer.Ordinal))
            {
                roles.Add(role, IsNull(grants) ? null : Sequence(grants, $"access.roles.{role}")
                    .Select((item, index) => Scalar(item, $"access.roles.{role}[{index}]")).ToList());
            }
        }

        AccessFileOutcome outcome = AccessFile.ValidatePolicy(permissions, requires, roles, fingerprint);
        return outcome.File ?? throw new MetadataFormatException(outcome.Problem ?? "The access policy is invalid.");
    }

    private static Dictionary<string, YamlNode> Mapping(YamlNode node, string path, StringComparer comparer)
    {
        if (node is not YamlMappingNode mapping)
        {
            throw new MetadataFormatException($"'{path}' must be a mapping.");
        }

        var result = new Dictionary<string, YamlNode>(comparer);
        foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
        {
            string? key = Scalar(entry.Key, $"{path} key");
            if (string.IsNullOrWhiteSpace(key) || !result.TryAdd(key, entry.Value))
            {
                throw new MetadataFormatException($"'{path}' contains an empty or duplicate key.");
            }
        }

        return result;
    }

    private static IEnumerable<YamlNode> Sequence(YamlNode node, string path) =>
        node is YamlSequenceNode sequence
            ? sequence.Children
            : throw new MetadataFormatException($"'{path}' must be a sequence.");

    private static string? OptionalScalar(Dictionary<string, YamlNode> mapping, string key) =>
        mapping.TryGetValue(key, out YamlNode? value) ? Scalar(value, key) : null;

    private static string? Scalar(YamlNode node, string path)
    {
        if (node is not YamlScalarNode scalar)
        {
            throw new MetadataFormatException($"'{Safe(path)}' must be a scalar.");
        }

        return IsNull(scalar) ? null : scalar.Value;
    }

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode scalar &&
        scalar.Style is ScalarStyle.Plain or ScalarStyle.Any &&
        (string.IsNullOrEmpty(scalar.Value) || scalar.Value == "~" ||
         scalar.Value.Equals("null", StringComparison.OrdinalIgnoreCase));

    private static void RejectUnknown(Dictionary<string, YamlNode> fields, HashSet<string> accepted, string path)
    {
        foreach (string key in fields.Keys)
        {
            if (!accepted.Contains(key))
            {
                throw new MetadataFormatException($"Unknown key '{Safe(key)}' in '{path}'.");
            }
        }
    }

    private static void ValidateNavigation(string key, string value)
    {
        bool valid = key switch
        {
            "order" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number),
            "hidden" or "topbar-hidden" => bool.TryParse(value, out _),
            "topbar-align" => value.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                              value.Equals("right", StringComparison.OrdinalIgnoreCase),
            "article-count" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "latest-article" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
            _ => true,
        };
        if (!valid)
        {
            throw new MetadataFormatException($"Navigation value '{key}' has an invalid type or value.");
        }
    }

    private static string Safe(string value)
    {
        string text = string.Concat(value.Take(160).Select(static c => char.IsControl(c) ? ' ' : c));
        return value.Length > 160 ? text + "..." : text;
    }

    private sealed class MetadataFormatException(string message) : Exception(message);
}
