// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public class Utils
{
    /// <summary>実行ファイルの名前の一覧のどれかのプロセスが持つ TCP 接続と、その実行ファイルの名前。</summary>
    public static List<(TcpHelper.TcpRow Row, string ExeName)> GetTCPConnectionsForExe(string[] filenames)
    {
        List<(TcpHelper.TcpRow Row, string ExeName)> tcpConns = [];

        var procs = GetProcessesFromList(filenames);
        Dictionary<int, string> exeNameByPid = [];
        foreach (var filename in filenames)
        {
            if (procs.TryGetValue(filename, out var process))
            {
                exeNameByPid.TryAdd(process.Id, filename);
            }
        }

        var tcpConnections = TcpHelper.GetExtendedTcpTable();
        foreach (var conn in tcpConnections) {
            if (exeNameByPid.TryGetValue(conn.owningPid, out var exeName)) {
                tcpConns.Add((conn, exeName));
            }
        }

        return tcpConns;
    }

    public static Dictionary<string, Process> GetProcessesFromList(string[] filenames)
    {
        var processesDict = new Dictionary<string, Process>();
        var processes = Process.GetProcesses();
        foreach (var process in processes)
        {
            if (filenames.Contains(process.ProcessName))
            {
                processesDict.TryAdd(process.ProcessName, process);
            }
        }

        return processesDict;
    }
}
