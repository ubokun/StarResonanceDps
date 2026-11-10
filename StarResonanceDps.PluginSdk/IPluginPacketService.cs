namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Reads the game messages the host captures.
/// </summary>
public interface IPluginPacketService
{
    /// <summary>
    /// Returns the captured messages of the given types, in the order they arrived, starting from the first message
    /// that arrives after the enumeration starts. The plugin receives messages only while the enumeration is running:
    /// it starts receiving when <c>await foreach</c> begins and stops when the loop exits.
    /// When the host stops the plugin, the enumeration ends without an exception, or with <see cref="OperationCanceledException"/>
    /// when <see cref="IPluginContext.Stopping"/> is passed as <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="types">
    /// The message types to read, at least one. A message is delivered when its kind, service ID and method ID all match one of them.
    /// </param>
    /// <param name="cancellationToken">
    /// When canceled, the enumeration ends with <see cref="OperationCanceledException"/> after the messages already received.
    /// </param>
    /// <remarks>
    /// Messages that have not been read yet are kept for the plugin. When they exceed 64 MB for one plugin,
    /// the host stops the plugin. Keep up with the messages or read only the types the plugin needs.
    /// <see cref="IStarResonancePlugin.Initialize"/> runs on the UI thread; start the loop with <c>ConfigureAwait(false)</c>
    /// so that its body does not run on the UI thread.
    /// </remarks>
    IAsyncEnumerable<PluginPacketMessage> ReadAsync(
        IReadOnlyCollection<PluginPacketType> types,
        CancellationToken cancellationToken = default);
}
