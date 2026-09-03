using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// KiiroSchizonepetaGum AssetBundle 导出脚本（放在 Unity 工程 Assets/Editor/ 下）。
/// 用法：Unity 菜单 → KiiroSchizonepetaGum → Build AssetBundle (Windows)。
///
/// 设计决策：描边特效仅支持 Windows，随包只发布 _win 版 shader AB 以减小包体。
/// - RimWorld 按当前 OS 匹配 AB 文件名后缀（ModAssetBundlesHandler.BundleSuffixForCurrentOs），
///   非 Windows 平台不会加载 kiirogum_win，配合 C# 端 KiiroGumOutlineManager 的平台判定
///   （非 Windows 整体不渲染描边），实现"仅 Windows 显示描边、其余平台静默无此特效"。
/// - 文件名必须带 _win 后缀：若为无后缀文件，所有平台都会尝试加载这份
///   D3D11 编译的 shader AB，导致 Linux/Mac 玩家加载失败或渲染错误。
///
/// 自动从工程位置向上查找 About/About.xml 定位 mod 根目录，
/// 把 Assets/Data/Cancelation.KiiroSchizonepetaGum/Materials/KiiroGumOutline.shader
/// （路径前缀 = packageId，见 AssetPaths 注释）打成
/// LZ4 压缩 AB，输出 kiirogum_win 到 mod 的 1.6/AssetBundles/。
/// </summary>
public static class BuildKiiroGumAssetBundles
{
    /// <summary>要打进 AB 的资源完整路径。必须与 RimWorld 的运行时查找规则匹配：
    /// ContentFinder.TryFindAssetInModBundles 依次尝试两个前缀（见反编译源码 L92-L118）：
    ///   1) Assets/Data/&lt;mod文件夹名&gt;/...   —— 本地开发文件夹名命中，但创意工坊订阅后
    ///      玩家侧文件夹名是 workshop id，该前缀失效；
    ///   2) Assets/Data/&lt;PackageIdPlayerFacing&gt;/... —— 取 About.xml 的 packageId
    ///      （本 mod 为 Cancelation.KiiroSchizonepetaGum），非官方 mod 必查，与文件夹名无关，
    ///      本地与创意工坊均稳定命中。
    /// 因此资源路径使用 packageId 前缀（不能硬编码文件夹名）。
    /// 后缀固定为 Materials/&lt;shaderPath&gt;.shader（GenFilePaths.ContentPath&lt;Shader&gt;）。</summary>
    private static readonly string[] AssetPaths =
    {
        "Assets/Data/Cancelation.KiiroSchizonepetaGum/Materials/KiiroGumOutline.shader"
    };

    /// <summary>bundle 名（带 RimWorld Windows 平台后缀，见类头注释）。</summary>
    private const string BundleNameWin = "kiirogum_win";

    /// <summary>导出菜单项：构建 Windows 版 AB 到 mod 的 1.6/AssetBundles/ 目录。</summary>
    [MenuItem("KiiroSchizonepetaGum/Build AssetBundle (Windows)")]
    public static void BuildAll()
    {
        string modRoot = FindModRoot();
        if (modRoot == null)
        {
            return;
        }

        string outDir = Path.Combine(modRoot, "1.6", "AssetBundles");

        // 清空输出目录后重建：避免残留旧产物被错误加载。
        // 重要：无后缀 bundle（旧 kiirogum）或 BuildPipeline 自动生成的总清单文件
        // "AssetBundles" 都无平台后缀，RimWorld 会尝试在【所有平台】加载它们，
        // 只留 kiirogum_win 才能保证"仅 Windows 有描边特效"。
        if (Directory.Exists(outDir))
        {
            Directory.Delete(outDir, true);
        }
        Directory.CreateDirectory(outDir);

        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = BundleNameWin,
            assetNames = AssetPaths
        };

        // ChunkBasedCompression = LZ4：RimWorld 可直接加载，体积小、加载快
        BuildPipeline.BuildAssetBundles(
            outDir,
            new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);

        // 清理 BuildAssetBundles 附带的 .manifest 描述文件（RimWorld 不需要，避免发布垃圾文件）
        foreach (string manifest in Directory.GetFiles(outDir, "*.manifest"))
        {
            File.Delete(manifest);
        }

        Debug.Log("[KiiroSchizonepetaGum] Windows AssetBundle 已导出：" + Path.Combine(outDir, BundleNameWin));
    }

    /// <summary>从工程 Assets 目录逐级向上查找 About/About.xml 定位 mod 根目录
    ///（本 Unity 工程约定放在 mod 文件夹内的 UnityProject/ 子目录）。</summary>
    /// <returns>mod 根目录；找不到时返回 null（已输出错误日志）。</returns>
    private static string FindModRoot()
    {
        DirectoryInfo dir = new DirectoryInfo(Application.dataPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "About", "About.xml")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        Debug.LogError("[KiiroSchizonepetaGum] 未找到 mod 根目录（向上查找 About/About.xml 失败）。" +
            "请确认本 Unity 工程位于 KiiroSchizonepetaGum mod 文件夹内。");
        return null;
    }
}
