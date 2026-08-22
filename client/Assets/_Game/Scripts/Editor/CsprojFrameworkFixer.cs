// 利用 AssetPostprocessor 在 Unity 每次生成 csproj 时，把 TargetFrameworkVersion 从 v4.7.1 改写为 v4.7.2，
// 解决 DOTween dll(4.7.2) 与工程文件(4.7.1) 不匹配的外部编译报错。
public class CsprojFrameworkFixer : UnityEditor.AssetPostprocessor
{
    public static string OnGeneratedCSProject(string path, string content)
        => content.Replace("<TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>",
                           "<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>");
}
