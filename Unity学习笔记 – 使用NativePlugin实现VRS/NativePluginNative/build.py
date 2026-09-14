import os
import sys
import shutil
import subprocess
from pathlib import Path

# ─── 路径配置 ───────────────────────────────────────────
SOURCE_DIR = Path(__file__).parent          # NativePluginNative/
OUTPUT_DIR = SOURCE_DIR / "build"
DLL_NAME   = "GfxPluginVRSPlugin.dll"

# Unity 工程的插件目录（DLL 最终部署位置）
UNITY_PLUGINS = SOURCE_DIR.parent / "Assets" / "Plugins"

# 编译的源文件（按需增删）
SOURCES = [
    SOURCE_DIR / "GfxPluginVRSPlugin.cpp",
    SOURCE_DIR / "RenderAPI_D3D12.cpp",
]


# ─── MSVC 工具链自动定位 ────────────────────────────────
VSWHERE = Path(r"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe")


def find_msvc():
    """用 vswhere.exe 找最新 Visual Studio 的 vcvars64.bat（不手写路径）"""
    if not VSWHERE.exists():
        sys.exit(f"未找到 vswhere.exe（{VSWHERE}），请确认已安装 Visual Studio / Build Tools")

    result = subprocess.run(
        [str(VSWHERE), "-latest", "-property", "installationPath"],
        capture_output=True, text=True)
    vs_path = result.stdout.strip()
    if not vs_path:
        sys.exit("未找到 Visual Studio 安装")

    # 优先选现有版本的实际宏
    vcvars = Path(vs_path) / "VC" / "Auxiliary" / "Build" / "vcvars64.bat"
    if not vcvars.exists():
        # 兼容以前的老布局（x86 安装路径）
        vcvars = Path(r"C:\Program Files (x86)" + vs_path[vs_path.find("\\Microsoft"):]) / \
                 "VC" / "Auxiliary" / "Build" / "vcvars64.bat"
    if not vcvars.exists():
        sys.exit(f"未找到 vcvars64.bat: {vcvars}")
    return str(vcvars)


def run_in_vs_env(vcvars, cmd_list, cwd=None):
    """在 VS 开发者环境中执行命令：cmd /c ""vcvars && <cmd>" """
    cmd_str = " && ".join(cmd_list)
    full_cmd = f'""{vcvars}" && {cmd_str}"'
    return subprocess.run(
        f"cmd /c {full_cmd}",
        cwd=str(cwd) if cwd else None,
        shell=True)


def locate_unity_includes():
    """返回 Unity 头文件 include 目录列表。

    检查当前工程目录下是否存在 Unity/ 头文件目录，
    找到就加入 -I，找不到则警告（真实编译还需要补齐头文件）。
    """
    candidates = [
        SOURCE_DIR / "Unity",                  # NativePluginNative/Unity/
        SOURCE_DIR.parent / "Unity",           # NativePluginVRS/Unity/
    ]
    found = [str(c) for c in candidates if c.is_dir()]
    return found


def copy_to_unity():
    """把编译好的 DLL 复制到 Unity 工程的 Assets/Plugins/。"""
    print("=" * 60)
    print("复制 DLL 到 Unity 工程...")
    print("=" * 60)

    src = OUTPUT_DIR / DLL_NAME
    if not src.exists():
        sys.exit(f"DLL 不存在: {src}，请先执行编译")

    UNITY_PLUGINS.mkdir(parents=True, exist_ok=True)

    dst = UNITY_PLUGINS / DLL_NAME
    shutil.copy2(src, dst)
    print(f"  {dst}")


def main():
    print("=" * 60)
    print("编译 DLL...")
    print("=" * 60)

    # 1) 工具链定位
    vcvars = find_msvc()
    print(f"  MSVC: {vcvars}")

    # 2) 源文件存在性
    for src in SOURCES:
        if not src.exists():
            sys.exit(f"源文件不存在: {src}")

    # 3) include 路径（Unity 头）
    includes = locate_unity_includes()
    if not includes:
        print("  ⚠ 未找到 Unity 头文件目录（Unity/），"
              "请补齐 IUnityInterface.h / IUnityGraphics.h / IUnityGraphicsD3D12.h")

    # 4) def 文件（链接用，可选）
    def_file = SOURCE_DIR / "GfxPluginVRSPlugin.def"
    if not def_file.exists():
        print("  ⚠ 未找到 GfxPluginVRSPlugin.def，链接可能失败（导出符号缺失）")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # 5) 编译
    obj_files = []
    for src in SOURCES:
        obj = OUTPUT_DIR / (src.stem + ".obj")
        obj_files.append(str(obj))
        inc_args = " ".join(f"/I{inc}" for inc in includes)
        compile_cmd = (
            f"cl /nologo /c /EHsc /std:c++17 /O2 "
            f"{inc_args} /Fo\"{obj}\" \"{src}\""
        )
        ret = run_in_vs_env(vcvars, [compile_cmd])
        if ret.returncode != 0:
            sys.exit(f"编译失败: {src}")

    # 6) 链接
    dll_path = OUTPUT_DIR / DLL_NAME
    def_arg = f"/DEF:\"{def_file}\"" if def_file.exists() else ""
    objs_arg = " ".join(f'"{o}"' for o in obj_files)
    link_cmd = (
        f"link /nologo /DLL /OUT:\"{dll_path}\" {def_arg} "
        f"{objs_arg} d3d12.lib dxgi.lib"
    )
    ret = run_in_vs_env(vcvars, [link_cmd])
    if ret.returncode != 0:
        sys.exit("链接失败")

    print(f"  {dll_path} ({dll_path.stat().st_size} bytes)")

    # 7) 复制到 Unity 工程
    print()
    copy_to_unity()

    print()
    print("✅ 构建完成!")


if __name__ == "__main__":
    main()