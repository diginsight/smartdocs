using System.Text;
using System.Text.Json;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Metadata;
using Diginsight.SmartDocs.Web.Shared.Navigation;

return await MetadataChecks.RunAsync(args);

internal static class MetadataChecks
{
    private static readonly MetadataParser Parser = new();
    private static int assertions;

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args is ["test"])
            {
                await TestAsync();
                Console.WriteLine($"PASS: {assertions} metadata assertions.");
                return 0;
            }

            if (args is ["validate-tree", var path])
            {
                return await ValidateTreeAsync(path);
            }

            Console.Error.WriteLine("Usage: MetadataChecks test | validate-tree <content-root>");
            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            Console.Error.WriteLine($"Metadata checks failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task TestAsync()
    {
        string fixtures = Path.Combine("eng", "metadata-checks", "fixtures");
        MetadataDocument combined = await ValidFileAsync(Path.Combine(fixtures, "combined-root", "metadata.yml"), true);
        Assert(combined.Versioned, "combined root uses the versioned schema");
        Assert(combined.Navigation.Label == "Example documentation", "navigation label");
        Assert(combined.Repository?.Id == "example/docs", "repository identity");
        Assert(combined.Repository?.InternalPeer["path_env"] == "EXAMPLE_INTERNAL_FOLDER", "peer data");
        Assert(combined.Repository?.DeliberatelyPublic.Length == 1, "public exceptions");
        Assert(combined.Access?.PermissionDescriptions["Read"] == "Read documentation", "permission descriptions retained");
        Assert(combined.Access?.Grants("reader").Contains("READ") == true, "case-insensitive role grants");
        Assert(combined.Navigation.Values?.ContainsKey("access") == false, "access omitted from navigation");
        Assert(combined.Navigation.Values?.ContainsKey("repository") == false, "repository omitted from navigation");

        MetadataDocument separateRepository = await ValidFileAsync(Path.Combine(fixtures, "separate-roots", "metadata.yml"), true);
        MetadataDocument separateContent = await ValidFileAsync(Path.Combine(fixtures, "separate-roots", "content", "metadata.yml"), true);
        Assert(separateRepository.Repository is not null && separateRepository.Access is null, "repository-only root");
        Assert(separateContent.Repository is null && separateContent.Access?.Requires.Contains("authenticated") == true, "separate content root");

        MetadataDocument legacy = await ValidFileAsync(Path.Combine(fixtures, "legacy-flat", "metadata.yml"), false);
        Assert(!legacy.Versioned && legacy.Navigation.Short == "Legacy", "legacy aliases");
        Assert(legacy.Navigation.Values?["custom-value"] == "Retained", "legacy scalar projection");
        Assert(legacy.Navigation.Values?.ContainsKey("access") == false && legacy.Warnings.Length == 1, "legacy reserved key excluded");
        MetadataDocument reserved = Parse("label: Safe\nrequires: Read\nvisibility: private\nroles: secret").Document!;
        Assert(reserved.Navigation.Label == "Safe" && reserved.Warnings.Length == 3 &&
            reserved.Navigation.Values?.Keys.All(key => !MetadataParser.IsReservedNavigationKey(key)) == true,
            "legacy policy and repository fields never reach navigation");

        AccessFileOutcome legacyPolicy = AccessFile.Parse(await File.ReadAllBytesAsync(
            Path.Combine(fixtures, "legacy-policy", "access.metadata.yml")));
        Assert(legacyPolicy.File is not null && MetadataPolicyComparer.Equivalent(combined.Access!, legacyPolicy.File), "equivalent policies");
        Assert(!MetadataPolicyComparer.Equivalent(combined.Access!, Parse("""
            schema: smartdocs-metadata/v1
            access:
              requires: [Read]
              permissions: {Read: Different}
              roles: {Reader: [Read]}
            """).Document!.Access!), "different policies conflict");

        Assert(Parse("schema: smartdocs-metadata/v1\naccess: {}").Document?.Access is not null, "explicit empty policy");
        Assert(Parse("schema: smartdocs-metadata/v1\nlabel: Navigation").Document?.Access is null, "missing policy");
        Invalid("schema: smartdocs-metadata/v1\naccess: null", true, "null access");
        Invalid("schema: smartdocs-metadata/v1\naccess:", true, "bare access");
        Invalid("schema: smartdocs-metadata/v2\naccess: {}", true, "unsupported version");
        Invalid("schema: smartdocs-metadata/v1\naccess: {requires: [Unknown]}", true, "unknown required permission");
        Invalid("schema: smartdocs-metadata/v1\naccess: {permissions: {authenticated: reserved}}", true, "reserved permission");
        Invalid("schema: smartdocs-metadata/v1\naccess: {roles: {Reader: [Unknown]}}", true, "unknown grant");
        Invalid("schema: smartdocs-metadata/v1\naccess: {permissions: {Read: x, read: y}}", true, "case-insensitive duplicate permission");
        Invalid("schema: smartdocs-metadata/v1\naccess: {roles: {Reader: [], reader: []}}", true, "case-insensitive duplicate role");
        Invalid("schema: smartdocs-metadata/v1\naccess: {unexpected: true}", true, "unknown policy field");
        Invalid("schema: smartdocs-metadata/v1\nrepository: null", true, "null repository");
        Invalid("schema: smartdocs-metadata/v1\nlabel: A\nnav-label: B", true, "conflicting aliases");
        Invalid("schema: smartdocs-metadata/v1\nlabel: A\nlabel: B", true, "duplicate YAML key");
        Invalid("label: A\nlabel: B\nschema: smartdocs-metadata/v1", true, "versioned duplicate before schema");
        Invalid("label: A\nlabel: B\naccess: {requires: [authenticated]}", true, "duplicate with structured legacy policy");
        Invalid("  label: A\n  label: B\n  schema: smartdocs-metadata/v1", true, "indented versioned duplicate before schema");
        Invalid("  label: A\n  label: B\n  access: {requires: [authenticated]}", true, "indented duplicate with structured legacy policy");
        Invalid("schema: smartdocs-metadata/v1\nhidden: perhaps", true, "invalid navigation value");
        Invalid("schema: smartdocs-metadata/v1\nunknown: value", true, "unknown versioned key");
        Invalid("schema: smartdocs-metadata/v1\nLabel: wrong case", true, "versioned key casing");
        Invalid("access: {requires: [authenticated]}", true, "unversioned structured policy");
        Invalid("schema: smartdocs-metadata/v1\naccess: {}", false, "nested access");
        Invalid("schema: smartdocs-metadata/v1\nrepository: {id: example}", false, "nested repository");
        Invalid("schema: smartdocs-metadata/v1\n---\nlabel: another", true, "multiple YAML documents");
        Assert(Parser.Parse([0xff], true).Status == MetadataStatus.Invalid, "invalid UTF-8");
        Assert(Parse("# no overrides\n").Status == MetadataStatus.Valid, "empty legacy metadata");
        const string legacyScalarSyntax = """
            label: Legacy # literal text
            short: null
            icon: ' spaced '
            order: 2 # not a number to the legacy parser
            custom-value: "literal\nvalue"
            """;
        FolderMeta originalScalars = FolderMeta.Parse(legacyScalarSyntax);
        FolderMeta? projectedScalars = Parse(legacyScalarSyntax, false).Document?.Navigation;
        Assert(projectedScalars?.Label == originalScalars.Label &&
            projectedScalars?.Short == originalScalars.Short &&
            projectedScalars?.Icon == originalScalars.Icon &&
            projectedScalars?.Order == originalScalars.Order &&
            projectedScalars?.Values?["custom-value"] == originalScalars.Values?["custom-value"],
            "legacy scalar syntax preserves the original values and navigation projection");
        const string fencedLegacy = "---\nlabel: Fenced legacy\n---\n# trailing comment\n";
        Assert(Parse(fencedLegacy, false).Document?.Navigation.Label == FolderMeta.Parse(fencedLegacy).Label,
            "legacy metadata accepts surrounding document fences");
        const string fencedDuplicates = """
            --- # opening fence
            label: First
            requires: private
            nav-label: Second
            label: Final # retained
            --- # closing fence
            """;
        MetadataDocument? fencedDocument = Parse(fencedDuplicates, false).Document;
        Assert(fencedDocument?.Navigation.Label == FolderMeta.Parse(fencedDuplicates).Label &&
            fencedDocument?.Navigation.Values?.ContainsKey("requires") == false &&
            fencedDocument?.Warnings.Length == 1,
            "fenced duplicate legacy keys preserve ordering without exposing reserved values");
        Invalid("label: first\n---\nlabel: second", false, "multiple legacy documents without a final fence");
        Invalid("label: first\n---\nlabel: second\n---", false, "multiple legacy YAML documents");
        Invalid("label: first\n...\n---\nlabel: second", false, "legacy documents after an explicit end");
        Invalid("---\nlabel: legacy\n---\naccess: {requires: [authenticated]}", true, "legacy fence cannot conceal a second policy document");
        Invalid("schema: smartdocs-metadata/v1\naccess: {}\n---", true, "versioned trailing document marker");
        Assert(Diginsight.SmartDocs.Web.Shared.Navigation.FolderMeta.Parse(
            "label: A\nnav-label: B\nlabel: C").Label == "C", "legacy repeated alias order");
        Assert(Parse("label: A\nnav-label: B\nlabel: C", false).Document?.Navigation.Label == "C",
            "shared parser preserves legacy repeated alias order");
        Assert(Parse("  label: A\n  nav-label: B\n  label: C", false).Document?.Navigation.Label == "C",
            "shared parser preserves indented legacy repeated alias order");
        const string repeatedInvalidValues = """
            order: 3
            order: invalid
            article-count: 12
            article-count: invalid
            latest-article: 2026-01-02
            latest-article: invalid
            """;
        var oldNavigation = Diginsight.SmartDocs.Web.Shared.Navigation.FolderMeta.Parse(repeatedInvalidValues);
        var sharedNavigation = Parse(repeatedInvalidValues, false).Document?.Navigation;
        Assert(sharedNavigation?.Order == oldNavigation.Order &&
            sharedNavigation?.ArticleCount == oldNavigation.ArticleCount &&
            sharedNavigation?.LatestArticleUtc == oldNavigation.LatestArticleUtc &&
            sharedNavigation?.Values?["order"] == "invalid",
            "legacy duplicates retain earlier valid numeric and date values without changing the last raw value");
        var repeatedReserved = Parse("label: Safe\nrequires: first\nrequires: second", false).Document;
        Assert(repeatedReserved?.Navigation.Label == "Safe" &&
            repeatedReserved.Navigation.Values?.ContainsKey("requires") == false &&
            repeatedReserved.Warnings.Length == 1,
            "legacy duplicate fallback excludes reserved fields from its navigation projection");
        await ExistingNavigationParityAsync();

        string beyondHead = "schema: smartdocs-metadata/v1\n" + string.Concat(Enumerable.Repeat("# padding\n", 900))
            + "access: {requires: [authenticated]}\n";
        Assert(Encoding.UTF8.GetByteCount(beyondHead) > 8192 &&
            Parse(beyondHead).Document?.Access?.Requires.Contains("authenticated") == true, "policy after 8 KiB");
        Assert(Parser.Parse(new byte[MetadataParser.MaxBytes + 1], true).Status == MetadataStatus.Invalid, "oversized input");
        Assert(await ValidateTreeAsync(Path.Combine(fixtures, "combined-root")) == 0, "valid tree");
        Assert(await ValidateTreeAsync(Path.Combine(fixtures, "invalid-alias"), quiet: true) != 0, "invalid alias tree");
        Assert(await ValidateTreeAsync(Path.Combine(fixtures, "invalid-policy"), quiet: true) != 0, "invalid policy tree");
        Assert(await ValidateTreeAsync(Path.Combine(fixtures, "nested-policy"), quiet: true) != 0, "nested policy tree");

        using JsonDocument schema = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            "src", "Diginsight.SmartDocs.Web", "Metadata", "metadata.schema.json")));
        Assert(schema.RootElement.GetProperty("$defs").GetProperty("access").GetProperty("type").GetString() == "object", "machine schema");
        await ProviderChecks.RunAsync(Assert);
    }

    private static async Task ExistingNavigationParityAsync()
    {
        string root = Path.GetFullPath(Path.Combine("src", "docs"));
        string[] files = Directory.GetFiles(root, MetadataParser.FileName, SearchOption.AllDirectories);
        Assert(files.Length > 0, "documentation metadata inventory is not empty");
        foreach (string file in files)
        {
            byte[] bytes = await File.ReadAllBytesAsync(file);
            bool isRoot = Path.GetDirectoryName(file)!.Equals(root, StringComparison.OrdinalIgnoreCase);
            MetadataOutcome outcome = Parser.Parse(bytes, isRoot);
            Assert(outcome.Status == MetadataStatus.Valid && outcome.Document is not null,
                $"existing metadata is valid: {file}");
            if (outcome.Document!.Versioned)
            {
                continue;
            }

            FolderMeta previous = FolderMeta.Parse(Encoding.UTF8.GetString(bytes));
            FolderMeta current = outcome.Document.Navigation;
            Assert(previous.Label == current.Label && previous.Short == current.Short &&
                previous.Icon == current.Icon && previous.Order.Equals(current.Order) &&
                previous.Hidden == current.Hidden && previous.TopbarHidden == current.TopbarHidden &&
                previous.TopbarAlign == current.TopbarAlign && previous.ArticleCount == current.ArticleCount &&
                previous.LatestArticleUtc == current.LatestArticleUtc,
                $"existing legacy navigation preserves its display values: {file}");

            var safePreviousValues = previous.Values?
                .Where(pair => !MetadataParser.IsReservedNavigationKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, string> currentValues = current.Values
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Assert(currentValues.Count == safePreviousValues.Count &&
                safePreviousValues.All(pair => currentValues.TryGetValue(pair.Key, out string? value) && value == pair.Value),
                $"existing legacy navigation preserves its safe projected values: {file}");
        }
    }

    private static async Task<int> ValidateTreeAsync(string path, bool quiet = false)
    {
        string root = Path.GetFullPath(path);
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException($"Content root '{path}' doesn't exist.");
        }

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(static file =>
                Path.GetFileName(file).Equals(MetadataParser.FileName, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file).Equals(AccessFile.FileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        MetadataDocument? rootDocument = null;
        AccessFile? legacyPolicy = null;
        int failures = 0;
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            bool isRoot = Path.GetDirectoryName(file)!.Equals(root,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            string relative = Path.GetRelativePath(root, file);
            string? problem = null;
            if (name.Equals(MetadataParser.FileName, StringComparison.OrdinalIgnoreCase))
            {
                if (!name.Equals(MetadataParser.FileName, StringComparison.Ordinal))
                {
                    problem = $"Rename the file to '{MetadataParser.FileName}'.";
                }
                else
                {
                    MetadataOutcome outcome = Parser.Parse(await File.ReadAllBytesAsync(file), isRoot);
                    problem = outcome.Problem;
                    if (isRoot && outcome.Status == MetadataStatus.Valid)
                    {
                        rootDocument = outcome.Document;
                    }
                }
            }
            else if (!name.Equals(AccessFile.FileName, StringComparison.Ordinal) || !isRoot)
            {
                problem = "A legacy access file must use its canonical name at the content root.";
            }
            else
            {
                AccessFileOutcome outcome = AccessFile.Parse(await File.ReadAllBytesAsync(file));
                problem = outcome.Problem;
                legacyPolicy = outcome.File;
            }

            if (problem is not null)
            {
                failures++;
                if (!quiet)
                {
                    Console.Error.WriteLine($"{relative}: {problem}");
                }
            }
        }

        if (rootDocument?.Access is { } canonical && legacyPolicy is not null &&
            !MetadataPolicyComparer.Equivalent(canonical, legacyPolicy))
        {
            failures++;
            if (!quiet)
            {
                Console.Error.WriteLine("Root canonical and legacy access policies conflict.");
            }
        }

        if (!quiet)
        {
            Console.WriteLine($"Checked {files.Length} metadata file(s); {failures} failure(s).");
        }

        return failures == 0 ? 0 : 1;
    }

    private static async Task<MetadataDocument> ValidFileAsync(string path, bool root)
    {
        MetadataOutcome result = Parser.Parse(await File.ReadAllBytesAsync(path), root);
        Assert(result.Status == MetadataStatus.Valid && result.Document is not null, $"{path}: {result.Problem}");
        return result.Document!;
    }

    private static MetadataOutcome Parse(string text, bool root = true) => Parser.Parse(Encoding.UTF8.GetBytes(text), root);

    private static void Invalid(string text, bool root, string scenario) =>
        Assert(Parse(text, root).Status == MetadataStatus.Invalid, scenario);

    private static void Assert(bool condition, string scenario)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {scenario}.");
        }
    }
}
