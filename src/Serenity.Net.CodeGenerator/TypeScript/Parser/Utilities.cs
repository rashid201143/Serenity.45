using System.Reflection.Metadata;
using static Serenity.TypeScript.NodeVisitor;
using static Serenity.TypeScript.Scanner;

namespace Serenity.TypeScript;

internal class Utilities
{
    public static int GetFullWidth(INode node)
    {
        return (node.End ?? 0) - (node.Pos ?? 0);
    }


    public static INode ContainsParseError(INode node)
    {
        AggregateChildData(node);

        return (node.Flags & NodeFlags.ThisNodeOrAnySubNodesHasError) != 0 ? node : null;
    }

    public static void AggregateChildData(INode node)
    {
        if ((node.Flags & NodeFlags.HasAggregatedChildData) != 0)
        {
            var thisNodeOrAnySubNodesHasError = (node.Flags & NodeFlags.ThisNodeHasError) != 0 ||
                                                ForEachChild(node, ContainsParseError) != null;
            if (thisNodeOrAnySubNodesHasError)
                node.Flags |= NodeFlags.ThisNodeOrAnySubNodesHasError;


            node.Flags |= NodeFlags.HasAggregatedChildData;
        }
    }


    public static bool NodeIsMissing(INode node)
    {
        if (node == null)
            return true;


        return node.Pos == node.End && node.Pos >= 0 && node.Kind != SyntaxKind.EndOfFileToken;
    }


    public static string GetTextOfNodeFromSourceText(string sourceText, INode node)
    {
        if (NodeIsMissing(node))
            return "";

        var start = SkipTrivia(sourceText, node.Pos ?? 0) ?? 0;

        if (node.End == null)
            return sourceText[start..];

        return sourceText[start..node.End.Value];
    }

    public static List<CommentRange> GetLeadingCommentRangesOfNodeFromText(INode node, string text)
    {
        return GetLeadingCommentRanges(text, node.Pos ?? 0);
    }

    public static List<CommentRange> GetJSDocCommentRanges(INode node, string text)
    {
        var commentRanges = node.Kind == SyntaxKind.Parameter ||
                            node.Kind == SyntaxKind.TypeParameter ||
                            node.Kind == SyntaxKind.FunctionExpression ||
                            node.Kind == SyntaxKind.ArrowFunction
            ? GetTrailingCommentRanges(text, node.Pos ?? 0).Concat(GetLeadingCommentRanges(text, node.Pos ?? 0))
            : GetLeadingCommentRangesOfNodeFromText(node, text);
        commentRanges ??= new List<CommentRange>();
        return commentRanges.Where(comment =>
                text[(comment.Pos ?? 0) + 1] == '*' &&
                text[(comment.Pos ?? 0) + 2] == '*' &&
                text[(comment.Pos ?? 0) + 3] != '/')
            .ToList();
    }



    public static bool IsModifierKind(SyntaxKind token)
    {
        return token switch
        {
            SyntaxKind.AbstractKeyword or SyntaxKind.AsyncKeyword or SyntaxKind.ConstKeyword or SyntaxKind.DeclareKeyword or SyntaxKind.DefaultKeyword or SyntaxKind.ExportKeyword or SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.ReadonlyKeyword or SyntaxKind.StaticKeyword => true,
            _ => false,
        };
    }


    public static bool IsParameterDeclaration(IVariableLikeDeclaration node)
    {
        var root = GetRootDeclaration(node);

        return root.Kind == SyntaxKind.Parameter;
    }


    public static INode GetRootDeclaration(INode node)
    {
        while (node.Kind == SyntaxKind.BindingElement)
            node = node.Parent.Parent;

        return node;
    }


    public static bool HasModifiers(INode node)
    {
        return GetModifierFlags(node) != ModifierFlags.None;
    }

    public static bool HasModifier(INode node, ModifierFlags flags)
    {
        return (GetModifierFlags(node) & flags) != 0;
    }

    public static ModifierFlags GetModifierFlags(INode node)
    {
        var flags = ModifierFlags.None;
        if (node is not IHasModifierLike hasModifiers || hasModifiers.Modifiers is null)
            return flags;

        foreach (var modifier in hasModifiers.Modifiers)
            flags |= ModifierToFlag(modifier.Kind);
        if ((node.Flags & NodeFlags.NestedNamespace) != 0 || node.Kind == SyntaxKind.Identifier &&
            ((Identifier)node).IsInJsDocNamespace)
            flags |= ModifierFlags.Export;

        return flags;
    }

    public static ModifierFlags ModifiersToFlags(IEnumerable<IModifierLike> modifiers)
    {
        var flags = ModifierFlags.None;
        if (modifiers != null)
        {
            foreach (var modifier in modifiers)
            {
                flags |= ModifierToFlag(modifier.Kind);
            }
        }
        return flags;
    }

    public static ModifierFlags ModifierToFlag(SyntaxKind token)
    {
        return token switch
        {
            SyntaxKind.StaticKeyword => ModifierFlags.Static,
            SyntaxKind.PublicKeyword => ModifierFlags.Public,
            SyntaxKind.ProtectedKeyword => ModifierFlags.Protected,
            SyntaxKind.PrivateKeyword => ModifierFlags.Private,
            SyntaxKind.AbstractKeyword => ModifierFlags.Abstract,
            SyntaxKind.ExportKeyword => ModifierFlags.Export,
            SyntaxKind.DeclareKeyword => ModifierFlags.Ambient,
            SyntaxKind.ConstKeyword => ModifierFlags.Const,
            SyntaxKind.DefaultKeyword => ModifierFlags.Default,
            SyntaxKind.AsyncKeyword => ModifierFlags.Async,
            SyntaxKind.ReadonlyKeyword => ModifierFlags.Readonly,
            _ => ModifierFlags.None,
        };
    }


    public static bool IsLogicalOperator(SyntaxKind token)
    {
        return token == SyntaxKind.BarBarToken
               || token == SyntaxKind.AmpersandAmpersandToken
               || token == SyntaxKind.ExclamationToken;
    }


    public static bool IsAssignmentOperator(SyntaxKind token)
    {
        return token >= SyntaxKind.FirstAssignment && token <= SyntaxKind.LastAssignment;
    }


    public static bool IsLeftHandSideExpressionKind(SyntaxKind kind)
    {
        return kind == SyntaxKind.PropertyAccessExpression
               || kind == SyntaxKind.ElementAccessExpression
               || kind == SyntaxKind.NewExpression
               || kind == SyntaxKind.CallExpression
               || kind == SyntaxKind.JsxElement
               || kind == SyntaxKind.JsxSelfClosingElement
               || kind == SyntaxKind.TaggedTemplateExpression
               || kind == SyntaxKind.ArrayLiteralExpression
               || kind == SyntaxKind.ParenthesizedExpression
               || kind == SyntaxKind.ObjectLiteralExpression
               || kind == SyntaxKind.ClassExpression
               || kind == SyntaxKind.FunctionExpression
               || kind == SyntaxKind.Identifier
               || kind == SyntaxKind.RegularExpressionLiteral
               || kind == SyntaxKind.NumericLiteral
               || kind == SyntaxKind.StringLiteral
               || kind == SyntaxKind.NoSubstitutionTemplateLiteral
               || kind == SyntaxKind.TemplateExpression
               || kind == SyntaxKind.FalseKeyword
               || kind == SyntaxKind.NullKeyword
               || kind == SyntaxKind.ThisKeyword
               || kind == SyntaxKind.TrueKeyword
               || kind == SyntaxKind.SuperKeyword
               || kind == SyntaxKind.NonNullExpression
               || kind == SyntaxKind.MetaProperty;
    }


    public static bool IsLeftHandSideExpression(IExpression node)
    {
        return IsLeftHandSideExpressionKind(SkipPartiallyEmittedExpressions(node).Kind);
    }

    public static ScriptKind EnsureScriptKind(string fileName, ScriptKind scriptKind)
    {
        // Using scriptKind as a condition handles both:
        // - 'scriptKind' is unspecified and thus it is `null`
        // - 'scriptKind' is set and it is `Unknown` (0)
        // If the 'scriptKind' is 'null' or 'Unknown' then we attempt
        // to get the ScriptKind from the file name. If it cannot be resolved
        // from the file name then the default 'TS' script kind is returned.
        var sk = scriptKind != ScriptKind.Unknown ? scriptKind : GetScriptKindFromFileName(fileName);
        return sk != ScriptKind.Unknown ? sk : ScriptKind.TS;
    }
    public static ScriptKind GetScriptKindFromFileName(string fileName)
    {
        //var ext = fileName.substr(fileName.LastIndexOf("."));
        var ext = System.IO.Path.GetExtension(fileName);
        return (ext?.ToLower()) switch
        {
            ".js" => ScriptKind.JS,
            ".jsx" => ScriptKind.JSX,
            ".ts" => ScriptKind.TS,
            ".tsx" => ScriptKind.TSX,
            _ => ScriptKind.Unknown,
        };
    }

    public static string NormalizePath(string path)
    {
        path = path.Replace('\\', '/');
        var rootLength = GetRootLength(path);
        var root = path[..rootLength];
        var normalized = GetNormalizedParts(path, rootLength);
        if (normalized.Count != 0)
        {
#if ISSOURCEGENERATOR
            var joinedParts = root + string.Join("/", normalized);
#else
            var joinedParts = root + string.Join('/', normalized);
#endif
            return path[^1] == '/' ? joinedParts + '/' : joinedParts;
        }
        else
        {
            return root;
        }
    }

    public static int GetRootLength(string path)
    {
        if (path[0] == '/')
        {
            if (path.Length < 2 || path[1] != '/')
                return 1;
            var p1 = path.IndexOf('/', 2);
            if (p1 < 0)
                return 2;
            var p2 = path.IndexOf('/', p1 + 1);
            if (p2 < 0)
                return p1 + 1;
            return p2 + 1;
        }

        if (path.Length > 1 && path[1] == ':')
        {
            if (path.Length > 2 && path[2] == '/')
                return 3;
            return 2;
        }

        if (path.LastIndexOf("file:///", 0, StringComparison.Ordinal) == 0)
            return "file:///".Length;
        var idx = path.IndexOf("://", StringComparison.Ordinal);
        if (idx != -1)
            return idx + "://".Length;
        return 0;
    }

    private static List<string> GetNormalizedParts(string normalizedSlashedPath, int rootLength)
    {
        var parts = normalizedSlashedPath[rootLength..].Split('/');
        List<string> normalized = [];
        foreach (var part in parts)
        {
            if (part == ".")
                continue;
            if (part == ".." && normalized.Count > 0 && normalized.LastOrDefault() != "..")
                normalized.RemoveAt(normalized.Count - 1);
            else if (!string.IsNullOrEmpty(part))
                normalized.Add(part);
        }
        return normalized;
    }

    public static bool FileExtensionIs(string path, string extension)
    {
        return path.EndsWith(extension, StringComparison.Ordinal);
    }

    public static Diagnostic CreateDetachedDiagnostic(string fileName, string sourceText, int start, int length, DiagnosticMessage message, object argument = null)
    {

        if ((start + length) > sourceText.Length)
        {
            length = sourceText.Length - start;
        }

        return new Diagnostic
        {
            FileName = fileName,
            Start = start,
            Length = length,
            Message = message
        };
    }

    public static Diagnostic CreateFileDiagnostic(SourceFile file, int start, int length, DiagnosticMessage message, object argument)
    {
        return new Diagnostic
        {
            File = file,
            Start = start,
            Length = length,
            Message = message,
            Argument = argument
        };
    }

    public static INode SkipPartiallyEmittedExpressions(INode node)
    {
        while (node.Kind == SyntaxKind.PartiallyEmittedExpression)
            node = ((PartiallyEmittedExpression)node).Expression;


        return node;
    }

    private static bool FileExtensionIsOneOf(string path, string[] extensions)
    {
        foreach (var extension in extensions)
        {
            if (FileExtensionIs(path, extension))
            {
                return true;
            }
        }
        return false;
    }

    static class Extension
    {
        public const string Ts = ".ts";
        public const string Tsx = ".tsx";
        public const string Dts = ".d.ts";
        public const string Js = ".js";
        public const string Jsx = ".jsx";
        public const string Json = ".json";
        public const string TsBuildInfo = ".tsbuildinfo";
        public const string Mjs = ".mjs";
        public const string Mts = ".mts";
        public const string Dmts = ".d.mts";
        public const string Cjs = ".cjs";
        public const string Cts = ".cts";
        public const string Dcts = ".d.cts";
    }

    static readonly string[] SupportedDeclarationExtensions = [".d.ts" /* Dts */, ".d.cts" /* Dcts */, ".d.mts" /* Dmts */];

    internal static bool IsDeclarationFileName(string fileName)
    {
        return FileExtensionIsOneOf(fileName, SupportedDeclarationExtensions) ||
            (FileExtensionIs(fileName, ".ts") && System.IO.Path.GetFileName(fileName).Contains(".d.", StringComparison.Ordinal));
    }

    internal static string IdText(Identifier identifier)
    {
        return identifier.Text;
        // return unescapeLeadingUnderscores(identifierOrPrivateName.escapedText);
    }

    internal static bool CanHaveModifiers(INode node)
    {
        var kind = node.Kind;
        return kind == SyntaxKind.TypeParameter
            || kind == SyntaxKind.Parameter
            || kind == SyntaxKind.PropertySignature
            || kind == SyntaxKind.PropertyDeclaration
            || kind == SyntaxKind.MethodSignature
            || kind == SyntaxKind.MethodDeclaration
            || kind == SyntaxKind.Constructor
            || kind == SyntaxKind.GetAccessor
            || kind == SyntaxKind.SetAccessor
            || kind == SyntaxKind.IndexSignature
            || kind == SyntaxKind.ConstructorType
            || kind == SyntaxKind.FunctionExpression
            || kind == SyntaxKind.ArrowFunction
            || kind == SyntaxKind.ClassExpression
            || kind == SyntaxKind.VariableStatement
            || kind == SyntaxKind.FunctionDeclaration
            || kind == SyntaxKind.ClassDeclaration
            || kind == SyntaxKind.InterfaceDeclaration
            || kind == SyntaxKind.TypeAliasDeclaration
            || kind == SyntaxKind.EnumDeclaration
            || kind == SyntaxKind.ModuleDeclaration
            || kind == SyntaxKind.ImportEqualsDeclaration
            || kind == SyntaxKind.ImportDeclaration
            || kind == SyntaxKind.ExportAssignment
            || kind == SyntaxKind.ExportDeclaration;
    }

    static bool IsImportEqualsDeclaration(INode node)
    {
        return node.Kind == SyntaxKind.ImportEqualsDeclaration;
    }

    static bool HasModifierOfKind(INode node, SyntaxKind kind)
    {
        return node is IHasModifierLike hasModifiers && hasModifiers.Modifiers != null && 
            hasModifiers.Modifiers.Any(m => m.Kind == kind);
    }

    static bool IsExternalModuleReference(INode node)
    {
        return node.Kind == SyntaxKind.ExternalModuleReference;
    }
    static bool IsImportDeclaration(INode node)
    {
        return node.Kind == SyntaxKind.ImportDeclaration;
    }

    static bool IsExportAssignment(INode node)
    {
        return node.Kind == SyntaxKind.ExportAssignment;
    }

    static bool IsExportDeclaration(INode node)
    {
        return node.Kind == SyntaxKind.ExportDeclaration;
    }

    static bool IsMetaProperty(INode node)
    {
        return node.Kind == SyntaxKind.MetaProperty;
    }

    static bool IsAnExternalModuleIndicatorNode(INode node)
    {
        return (CanHaveModifiers(node) && HasModifierOfKind(node, SyntaxKind.ExportKeyword))
            || (IsImportEqualsDeclaration(node) && IsExternalModuleReference((node as ImportEqualsDeclaration)?.ModuleReference))
            || IsImportDeclaration(node)
            || IsExportAssignment(node)
            || IsExportDeclaration(node);
    }

    static INode GetImportMetaIfNecessary(SourceFile sourceFile)
    {
        return (sourceFile.Flags & NodeFlags.PossiblyContainsImportMeta) != 0 ?
            WalkTreeForImportMeta(sourceFile) :
            null;
    }

    static INode WalkTreeForImportMeta(INode node)
    {
        return IsImportMeta(node) ? node : ForEachChild(node, WalkTreeForImportMeta);
    }

    static bool IsImportMeta(INode node)
    {
        return IsMetaProperty(node) && node is MetaProperty { KeywordToken: SyntaxKind.ImportKeyword } mp && mp.Name?.EscapedText == "meta";
    }

    internal static INode IsFileProbablyExternalModule(SourceFile sourceFile)
    {
        // Try to use the first top-level import/export when available, then
        // fall back to looking for an 'import.meta' somewhere in the tree if necessary.
        return sourceFile.Statements.FirstOrDefault(IsAnExternalModuleIndicatorNode) ??
            GetImportMetaIfNecessary(sourceFile);
    }

    internal static void SetExternalModuleIndicator(SourceFile sourceFile)
    {
        sourceFile.ExternalModuleIndicator = IsFileProbablyExternalModule(sourceFile);
    }

    internal static ITextRange SetTextRangePosEnd(ITextRange range, int pos, int end)
    {
        range.Pos = pos;
        range.End = end;
        return range;
    }

    internal static ITextRange SetTextRangePosWidth(ITextRange range, int pos, int width)
    {
        return SetTextRangePosEnd(range, pos, pos + width);
    }

    internal static ITextRange SetTextRange(ITextRange range, ITextRange location)
    {
        return location != null ? SetTextRangePosEnd(range, location.Pos ?? 0, location.End ?? location.Pos ?? 0) : range;
    }

    internal static bool IsExternalModule(SourceFile sourceFile)
    {
        return sourceFile.ExternalModuleIndicator != null;
    }

    internal static bool CanHaveJSDoc(INode node)
    {
        return node.Kind switch
        {
            SyntaxKind.ArrowFunction or SyntaxKind.BinaryExpression or SyntaxKind.Block or SyntaxKind.BreakStatement or
            SyntaxKind.CallSignature or SyntaxKind.CaseClause or SyntaxKind.ClassDeclaration or SyntaxKind.ClassExpression or
            SyntaxKind.ClassStaticBlockDeclaration or SyntaxKind.Constructor or SyntaxKind.ConstructorType or
            SyntaxKind.ConstructSignature or SyntaxKind.ContinueStatement or SyntaxKind.DebuggerStatement or
            SyntaxKind.DoStatement or SyntaxKind.ElementAccessExpression or SyntaxKind.EmptyStatement or
            SyntaxKind.EndOfFileToken or SyntaxKind.EnumDeclaration or SyntaxKind.EnumMember or SyntaxKind.ExportAssignment or
            SyntaxKind.ExportDeclaration or SyntaxKind.ExportSpecifier or SyntaxKind.ExpressionStatement or
            SyntaxKind.ForInStatement or SyntaxKind.ForOfStatement or SyntaxKind.ForStatement or SyntaxKind.FunctionDeclaration or
            SyntaxKind.FunctionExpression or SyntaxKind.FunctionType or SyntaxKind.GetAccessor or SyntaxKind.Identifier or
            SyntaxKind.IfStatement or SyntaxKind.ImportDeclaration or SyntaxKind.ImportEqualsDeclaration or SyntaxKind.IndexSignature or
            SyntaxKind.InterfaceDeclaration or SyntaxKind.JSDocFunctionType or SyntaxKind.JSDocSignature or SyntaxKind.LabeledStatement or
            SyntaxKind.MethodDeclaration or SyntaxKind.MethodSignature or SyntaxKind.ModuleDeclaration or SyntaxKind.NamedTupleMember or
            SyntaxKind.NamespaceExportDeclaration or SyntaxKind.ObjectLiteralExpression or SyntaxKind.Parameter or
            SyntaxKind.ParenthesizedExpression or SyntaxKind.PropertyAccessExpression or SyntaxKind.PropertyAssignment or
            SyntaxKind.PropertyDeclaration or SyntaxKind.PropertySignature or SyntaxKind.ReturnStatement or SyntaxKind.SemicolonClassElement or
            SyntaxKind.SetAccessor or SyntaxKind.ShorthandPropertyAssignment or SyntaxKind.SpreadAssignment or
            SyntaxKind.SwitchStatement or SyntaxKind.ThrowStatement or SyntaxKind.TryStatement or SyntaxKind.TypeAliasDeclaration or
            SyntaxKind.TypeParameter or SyntaxKind.VariableDeclaration or SyntaxKind.VariableStatement or SyntaxKind.WhileStatement or
            SyntaxKind.WithStatement => true,
            _ => false,
        };
    }
}
