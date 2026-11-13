#!/usr/bin/env bash
# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

# 配布物のzip(StarResonanceDps-<版>.zip)とSDKのパッケージ(.nupkg)を作る。Git Bashで動かす。
# 使い方: ./release.sh [出力先](PowerShell・コマンドプロンプトからは、同じ場所のrelease.cmd [出力先])
# 出力先を省くと、このスクリプトと同じ場所のreleaseフォルダに作る(標準の.gitignoreで除外される名前)。

set -euo pipefail

if [ "$#" -gt 1 ]; then
  echo "使い方: ./release.sh [出力先]" >&2
  exit 2
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# DLL・exeのソースのパスを/_に置き換え、手元のパスを残さない。
path_map="-p:PathMap=$(cygpath -w "$root")=/_"

read_version() {
  local version
  version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$1" | head -n 1)"
  if [ -z "$version" ]; then
    echo "${1}に<Version>がありません。" >&2
    exit 1
  fi
  echo "$version"
}

app_version="$(read_version "$root/StarResonanceDps.App/StarResonanceDps.App.csproj")"
sdk_version="$(read_version "$root/StarResonanceDps.PluginSdk/StarResonanceDps.PluginSdk.csproj")"

out_arg="${1:-$root/release}"
mkdir -p "$out_arg"
out="$(cd "$out_arg" && pwd)"
publish_dir="$out/StarResonanceDps-$app_version"
zip_path="$out/StarResonanceDps-$app_version.zip"
nupkg_path="$out/StarResonanceDps.PluginSdk.$sdk_version.nupkg"

# 前の出力が残っていると、update-files.txtとzipに入る。
rm -rf "$publish_dir" "$zip_path"

dotnet publish "$root/StarResonanceDps.App/StarResonanceDps.App.csproj" -c Release -r win-x64 --self-contained false -o "$publish_dir" "$path_map"

# アプリの更新は、zipの直下のexeとupdate-files.txtを読む。
for required in StarResonanceDps.exe update-files.txt; do
  if [ ! -f "$publish_dir/$required" ]; then
    echo "公開の出力に${required}がありません。" >&2
    exit 1
  fi
done

powershell.exe -NoProfile -Command "\$ErrorActionPreference = 'Stop'; Compress-Archive -Path '$(cygpath -w "$publish_dir")\*' -DestinationPath '$(cygpath -w "$zip_path")'"

# 残すのはzipとSDKのパッケージだけ。公開の出力はzipの中にある。
rm -rf "$publish_dir"

dotnet pack "$root/StarResonanceDps.PluginSdk/StarResonanceDps.PluginSdk.csproj" -c Release -o "$out" "$path_map"

echo
echo "できたもの:"
echo "  $(cygpath -w "$zip_path")"
echo "  $(cygpath -w "$nupkg_path")"
echo "GitHubのReleasesで、タグv${app_version}を作り、この2つを上げて公開してください。"
