// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics.CodeAnalysis;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 本体がプラグインのコードを呼ぶ所(初期化・終わり・窓の中身を作る・後始末・本体の知らせ)を、全部この1つの受け止めで呼ぶ。
/// 例外はプラグインごとの受け手(<see cref="PluginManager"/>。ログに書き、動いていればそのプラグインを止める)へ渡し、本体へは投げない。
/// </summary>
internal sealed class PluginCallGuard
{
    private readonly Action<string, Exception> _onFailure;

    public PluginCallGuard(Action<string, Exception> onFailure)
    {
        _onFailure = onFailure ?? throw new ArgumentNullException(nameof(onFailure));
    }

    /// <param name="operation">ログに書く呼んだ処理の名前(英語)。</param>
    public bool TryInvoke(string operation, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception)
        {
            _onFailure(operation, exception);
            return false;
        }
    }

    public bool TryInvoke<T>(string operation, Func<T> function, [MaybeNullWhen(false)] out T result)
    {
        try
        {
            result = function();
            return true;
        }
        catch (Exception exception)
        {
            _onFailure(operation, exception);
            result = default;
            return false;
        }
    }
}
