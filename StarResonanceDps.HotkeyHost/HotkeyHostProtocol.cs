// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.HotkeyHost;

/// <summary>
/// 本体とホットキーの補助(StarResonanceDps.HotkeyHost.exe)の取り決め。本体もこのファイルをリンクで取り込む。
///
/// <para>
/// 本体が名前付きパイプのサーバーになり、補助を「パイプ名 本体のプロセスID」の引数で起動してつながせる。
/// 中身は1行1命令の文字列で、数は10進。
/// 本体 → 補助: <c>R 番号 修飾 仮想キー</c>(割り当てを足す)/ <c>U 番号</c>(外す)/
/// <c>N</c>(ゲームのプロセス名を空にする)/ <c>G 名前</c>(ゲームのプロセス名を1つ足す。名前は行の残り全部)/
/// <c>B 窓のハンドル</c>(本体の窓を前面の窓のすぐ後ろへ置く。前面の窓が無いかその窓自身なら何もしない)。
/// B はアクティブにはせず、本体のプロセスの窓だけ受け付ける。
/// 修飾と仮想キーは <c>RegisterHotKey</c> と同じ値。
/// 補助 → 本体: 1命令に1つ <c>K エラー番号</c>(0 は成功、それ以外は Windows のエラー番号)。
/// キーが押されたら <c>P 番号</c>(返事の合間にも挟まる)。
/// </para>
/// </summary>
internal static class HotkeyHostProtocol
{
    public const string ExecutableFileName = "StarResonanceDps.HotkeyHost.exe";

    public const string Register = "R";
    public const string Unregister = "U";
    public const string ClearGameProcessNames = "N";
    public const string AddGameProcessName = "G";
    public const string PlaceBehindForeground = "B";
    public const string Reply = "K";
    public const string Pressed = "P";

    // 補助の終了コード。0 は本体がパイプを閉じて普通に終わったとき。
    public const int ExitInvalidArguments = 1;
    public const int ExitConnectFailed = 2;
    public const int ExitUnexpectedServer = 3;
    public const int ExitInvalidCommand = 4;
}
