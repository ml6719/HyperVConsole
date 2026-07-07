using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HyperVConsoleKit;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var client = new HyperVConsoleClient();

app.UseWebSockets();

app.MapGet("/", () => Results.Content(GetHtml(), "text/html; charset=utf-8"));

app.MapGet("/api/vms", () => client.GetVirtualMachines());

app.MapGet("/api/vms/{id:guid}/capabilities", (Guid id) => client.GetConsoleCapabilities(id));

app.MapPost("/api/vms/{id:guid}/enhanced-session", (Guid id) =>
{
    return client.TryLaunchEnhancedSession(id) ? Results.Ok() : Results.BadRequest(client.GetEnhancedSessionLaunchInfo(id));
});

app.MapPost("/api/vms/{id:guid}/start", (Guid id) =>
{
    client.StartVirtualMachine(id);
    return Results.Ok();
});

app.MapPost("/api/vms/{id:guid}/stop", (Guid id) =>
{
    client.StopVirtualMachine(id, false);
    return Results.Ok();
});

app.MapPost("/api/vms/{id:guid}/reset", (Guid id) =>
{
    client.ResetVirtualMachine(id);
    return Results.Ok();
});

app.MapPost("/api/vms/{id:guid}/keys/{key}", (Guid id, string key) =>
{
    using var session = client.OpenConsole(id);
    SendKeyCommand(session, key);
    return Results.Ok();
});

app.MapPost("/api/vms/{id:guid}/text", async (Guid id, HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var text = await reader.ReadToEndAsync();
    using var session = client.OpenConsole(id);
    session.SendText(text);
    return Results.Ok();
});

app.MapPost("/api/vms/{id:guid}/paste", async (Guid id, HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var text = await reader.ReadToEndAsync();
    using var session = client.OpenConsole(id, new HyperVConsoleOpenOptions { Mode = HyperVConsoleMode.RawHostConsole });
    await session.PasteTextAsKeystrokesAsync(text, new ConsolePasteOptions(), request.HttpContext.RequestAborted);
    return Results.Ok();
});

app.MapPost("/api/vms/{id:guid}/mouse/click", (Guid id, int x, int y, MouseButton button) =>
{
    using var session = client.OpenConsole(id, new HyperVConsoleOpenOptions { Mode = HyperVConsoleMode.RawHostConsole });
    return session.TrySendMouseClick(x, y, button) ? Results.Ok() : Results.BadRequest();
});

app.Map("/ws/console/{id:guid}", async (Guid id, HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var fps = 5.0;
    if (double.TryParse(context.Request.Query["fps"], out var requestedFps))
    {
        fps = requestedFps;
    }

    var width = 1024;
    if (int.TryParse(context.Request.Query["width"], out var requestedWidth))
    {
        width = requestedWidth;
    }

    var height = 768;
    if (int.TryParse(context.Request.Query["height"], out var requestedHeight))
    {
        height = requestedHeight;
    }

    var preset = ConsoleStreamPreset.Custom;
    if (Enum.TryParse<ConsoleStreamPreset>(context.Request.Query["preset"], true, out var requestedPreset))
    {
        preset = requestedPreset;
    }

    var options = preset == ConsoleStreamPreset.Custom
        ? new ConsoleFrameStreamOptions()
        : ConsoleFrameStreamOptions.CreatePreset(preset);

    options.Width = width;
    options.Height = height;
    options.ActiveFramesPerSecond = fps;
    options.FramesPerSecond = fps;

    if (double.TryParse(context.Request.Query["idleFps"], out var idleFps))
    {
        options.IdleFramesPerSecond = idleFps;
    }

    if (Enum.TryParse<ConsoleFramePixelFormat>(context.Request.Query["format"], true, out var pixelFormat))
    {
        options.PixelFormat = pixelFormat;
    }

    if (long.TryParse(context.Request.Query["maxBps"], out var maxBytesPerSecond))
    {
        options.MaxBytesPerSecond = maxBytesPerSecond;
    }

    if (bool.TryParse(context.Request.Query["tiles"], out var sendTiles))
    {
        options.SendChangedTilesOnly = sendTiles;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var session = client.OpenConsole(id);

    await session.StreamFramesAsync(options, async (frame, cancellationToken) =>
    {
        if (socket.State == WebSocketState.Open)
        {
            await SendConsoleFrameAsync(socket, frame, cancellationToken);
        }
    }, context.RequestAborted);
});

app.Run();

static void SendKeyCommand(IHyperVConsoleSession session, string key)
{
    switch (key.ToLowerInvariant())
    {
        case "enter":
            session.SendKey(ConsoleKeyCode.Enter);
            break;
        case "escape":
            session.SendKey(ConsoleKeyCode.Escape);
            break;
        case "tab":
            session.SendKey(ConsoleKeyCode.Tab);
            break;
        case "up":
            session.SendKey(ConsoleKeyCode.Up);
            break;
        case "down":
            session.SendKey(ConsoleKeyCode.Down);
            break;
        case "left":
            session.SendKey(ConsoleKeyCode.Left);
            break;
        case "right":
            session.SendKey(ConsoleKeyCode.Right);
            break;
        case "f8":
            session.SendKey(ConsoleKeyCode.F8);
            break;
        case "f12":
            session.SendKey(ConsoleKeyCode.F12);
            break;
        case "ctrlaltdel":
            session.SendCtrlAltDel();
            break;
        default:
            throw new InvalidOperationException("Unsupported key command: " + key);
    }
}

static async Task SendConsoleFrameAsync(WebSocket socket, ConsoleFrame frame, CancellationToken cancellationToken)
{
    var payload = frame.UpdateKind == ConsoleFrameUpdateKind.FullFrame
        ? frame.RawBytes ?? Array.Empty<byte>()
        : ConcatTiles(frame.Tiles);
    var offset = 0;
    var header = new
    {
        sequenceNumber = frame.SequenceNumber,
        width = frame.Width,
        height = frame.Height,
        pixelFormat = frame.PixelFormat.ToString(),
        updateKind = frame.UpdateKind.ToString(),
        isKeyFrame = frame.IsKeyFrame,
        payloadBytes = payload.Length,
        targetFramesPerSecond = frame.TargetFramesPerSecond,
        tiles = frame.Tiles.Select(tile =>
        {
            var item = new
            {
                x = tile.X,
                y = tile.Y,
                width = tile.Width,
                height = tile.Height,
                offset,
                length = tile.RawBytes.Length
            };
            offset += tile.RawBytes.Length;
            return item;
        }).ToArray()
    };

    var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);
    var message = new byte[4 + headerBytes.Length + payload.Length];
    message[0] = (byte)(headerBytes.Length & 0xFF);
    message[1] = (byte)((headerBytes.Length >> 8) & 0xFF);
    message[2] = (byte)((headerBytes.Length >> 16) & 0xFF);
    message[3] = (byte)((headerBytes.Length >> 24) & 0xFF);
    Buffer.BlockCopy(headerBytes, 0, message, 4, headerBytes.Length);
    Buffer.BlockCopy(payload, 0, message, 4 + headerBytes.Length, payload.Length);
    await socket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken);
}

static byte[] ConcatTiles(IReadOnlyList<ConsoleFrameTile> tiles)
{
    var total = tiles.Sum(tile => tile.RawBytes.Length);
    var output = new byte[total];
    var offset = 0;
    foreach (var tile in tiles)
    {
        Buffer.BlockCopy(tile.RawBytes, 0, output, offset, tile.RawBytes.Length);
        offset += tile.RawBytes.Length;
    }

    return output;
}

static string GetHtml()
{
    return """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>HyperVConsoleKit Sample</title>
  <style>
    :root { color-scheme: dark; font-family: Segoe UI, system-ui, sans-serif; background: #111318; color: #f6f1e8; }
    * { box-sizing: border-box; }
    body { margin: 0; min-height: 100vh; display: grid; grid-template-columns: 320px 1fr; }
    aside { border-right: 1px solid #343946; padding: 18px; background: #191c22; overflow: auto; }
    main { display: grid; grid-template-rows: auto 1fr auto; min-width: 0; }
    h1 { font-size: 18px; margin: 0 0 4px; font-weight: 650; }
    .sample { color: #f4c95d; font-size: 13px; line-height: 1.35; margin: 0 0 18px; }
    .vm-list { display: grid; gap: 8px; }
    .vm { width: 100%; text-align: left; border: 1px solid #373d49; background: #22262f; color: inherit; padding: 10px; border-radius: 6px; cursor: pointer; }
    .vm[aria-selected="true"] { border-color: #79c7c5; background: #26343a; }
    .vm strong { display: block; font-size: 14px; overflow-wrap: anywhere; }
    .vm span { color: #bec7d5; font-size: 12px; }
    header, footer { padding: 12px 16px; border-bottom: 1px solid #303642; display: flex; gap: 8px; align-items: center; flex-wrap: wrap; background: #151820; }
    footer { border-top: 1px solid #303642; border-bottom: 0; }
    button { border: 1px solid #46505f; background: #252b35; color: #f6f1e8; border-radius: 6px; min-height: 34px; padding: 0 11px; cursor: pointer; }
    button:hover { border-color: #79c7c5; }
    button.danger { border-color: #8f4d57; }
    input { min-height: 34px; border: 1px solid #46505f; background: #0f1218; color: #f6f1e8; border-radius: 6px; padding: 0 10px; min-width: min(360px, 100%); }
    .viewer { min-height: 0; display: grid; place-items: center; padding: 14px; background: #0b0d11; }
    canvas { width: min(100%, calc((100vh - 148px) * 1.333)); aspect-ratio: 4 / 3; background: #050608; border: 1px solid #313744; }
    .status { margin-left: auto; color: #9fb0c8; font-size: 13px; }
    @media (max-width: 860px) {
      body { grid-template-columns: 1fr; grid-template-rows: auto 1fr; }
      aside { border-right: 0; border-bottom: 1px solid #343946; max-height: 240px; }
    }
  </style>
</head>
<body>
  <aside>
    <h1>HyperVConsoleKit</h1>
    <p class="sample">Sample console gateway only. Add authentication, authorization, audit logging, TLS, and approval before any real remote use.</p>
    <div id="vms" class="vm-list"></div>
  </aside>
  <main>
    <header>
      <button data-key="tab">Tab</button>
      <button data-key="enter">Enter</button>
      <button data-key="escape">Esc</button>
      <button data-key="up">Up</button>
      <button data-key="down">Down</button>
      <button data-key="left">Left</button>
      <button data-key="right">Right</button>
      <button data-key="f8">F8</button>
      <button data-key="f12">F12</button>
      <button class="danger" data-key="ctrlaltdel">Ctrl Alt Del</button>
      <span id="status" class="status">Disconnected</span>
    </header>
    <section class="viewer">
      <canvas id="screen" width="1024" height="768"></canvas>
    </section>
    <footer>
      <input id="text" autocomplete="off" placeholder="Text to type">
      <button id="sendText">Send</button>
      <button id="pasteText">Paste</button>
      <input id="fps" type="number" min="1" max="30" step="1" value="5" title="Frames per second">
      <input id="idleFps" type="number" min="0.2" max="30" step="0.2" value="1" title="Idle frames per second">
      <input id="maxBps" type="number" min="0" step="10000" value="500000" title="Max bytes per second">
      <select id="format" title="Pixel format">
        <option value="Rgb332">RGB332</option>
        <option value="Rgb565">RGB565</option>
        <option value="Gray8">Gray8</option>
        <option value="Gray4">Gray4</option>
        <option value="Mono1">Mono1</option>
      </select>
      <select id="preset" title="Stream preset">
        <option value="Custom">Custom</option>
        <option value="Latency">Latency</option>
        <option value="Balanced">Balanced</option>
        <option value="LowBandwidth">Low bandwidth</option>
        <option value="Quality">Quality</option>
      </select>
      <label><input id="tiles" type="checkbox" checked> Tiles</label>
      <button id="reconnect">Reconnect</button>
      <button id="start">Start</button>
      <button id="stop">Stop</button>
      <button id="reset" class="danger">Reset</button>
      <button id="enhanced">Enhanced</button>
    </footer>
  </main>
  <script>
    const list = document.getElementById('vms');
    const statusEl = document.getElementById('status');
    const canvas = document.getElementById('screen');
    const ctx = canvas.getContext('2d');
    let selected;
    let socket;

    async function loadVms() {
      const vms = await fetch('/api/vms').then(r => r.json());
      list.replaceChildren(...vms.map(vm => {
        const button = document.createElement('button');
        button.className = 'vm';
        button.setAttribute('aria-selected', selected && selected.id === vm.id ? 'true' : 'false');
        button.innerHTML = `<strong>${escapeHtml(vm.name)}</strong><span>${vm.state} | ${vm.id}</span>`;
        button.onclick = () => selectVm(vm);
        return button;
      }));
      if (!selected && vms.length) selectVm(vms.find(v => v.isRunning) || vms[0]);
    }

    function selectVm(vm) {
      selected = vm;
      [...list.children].forEach(child => child.setAttribute('aria-selected', child.textContent.includes(vm.id) ? 'true' : 'false'));
      if (socket) socket.close();
      const width = canvas.width;
      const height = canvas.height;
      const fps = document.getElementById('fps').value || '5';
      const idleFps = document.getElementById('idleFps').value || '1';
      const maxBps = document.getElementById('maxBps').value || '';
      const format = document.getElementById('format').value;
      const preset = document.getElementById('preset').value;
      const tiles = document.getElementById('tiles').checked;
      const scheme = location.protocol === 'https:' ? 'wss' : 'ws';
      const params = new URLSearchParams({ width, height, fps, idleFps, format, preset, tiles });
      if (maxBps) params.set('maxBps', maxBps);
      socket = new WebSocket(`${scheme}://${location.host}/ws/console/${vm.id}?${params}`);
      socket.binaryType = 'arraybuffer';
      socket.onopen = () => statusEl.textContent = `${vm.name} connected`;
      socket.onclose = () => statusEl.textContent = 'Disconnected';
      socket.onerror = () => statusEl.textContent = 'Stream error';
      socket.onmessage = event => {
        const frame = parseFrame(event.data);
        drawFrame(frame);
        statusEl.textContent = `${vm.name} ${frame.header.pixelFormat} ${frame.header.updateKind} ${Math.round(frame.header.payloadBytes / 1024)} KB`;
      };
    }

    canvas.onclick = event => {
      if (!selected) return;
      const rect = canvas.getBoundingClientRect();
      const x = Math.round(((event.clientX - rect.left) / rect.width) * 32767);
      const y = Math.round(((event.clientY - rect.top) / rect.height) * 32767);
      post(`/api/vms/{id}/mouse/click?x=${x}&y=${y}&button=Left`);
    };

    async function post(path, body) {
      if (!selected) return;
      await fetch(path.replace('{id}', selected.id), { method: 'POST', body });
      setTimeout(loadVms, 500);
    }

    document.querySelectorAll('[data-key]').forEach(button => {
      button.onclick = () => post(`/api/vms/{id}/keys/${button.dataset.key}`);
    });
    document.getElementById('sendText').onclick = () => post('/api/vms/{id}/text', document.getElementById('text').value);
    document.getElementById('pasteText').onclick = () => post('/api/vms/{id}/paste', document.getElementById('text').value);
    document.getElementById('reconnect').onclick = () => selected && selectVm(selected);
    document.getElementById('start').onclick = () => post('/api/vms/{id}/start');
    document.getElementById('stop').onclick = () => post('/api/vms/{id}/stop');
    document.getElementById('reset').onclick = () => post('/api/vms/{id}/reset');
    document.getElementById('enhanced').onclick = () => post('/api/vms/{id}/enhanced-session');

    function escapeHtml(value) {
      return value.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function parseFrame(buffer) {
      const view = new DataView(buffer);
      const headerLength = view.getUint32(0, true);
      const headerBytes = new Uint8Array(buffer, 4, headerLength);
      const header = JSON.parse(new TextDecoder().decode(headerBytes));
      const payload = new Uint8Array(buffer, 4 + headerLength);
      return { header, payload };
    }

    function drawFrame(frame) {
      if (frame.header.updateKind === 'FullFrame') {
        drawPixels(frame.payload, frame.header.pixelFormat, frame.header.width, frame.header.height, 0, 0);
        return;
      }

      for (const tile of frame.header.tiles) {
        drawPixels(frame.payload.subarray(tile.offset, tile.offset + tile.length), frame.header.pixelFormat, tile.width, tile.height, tile.x, tile.y);
      }
    }

    function drawPixels(source, format, width, height, x, y) {
      const image = ctx.createImageData(width, height);
      let dst = 0;
      for (let i = 0; i < width * height; i++) {
        const pixel = decodePixel(source, i, format);
        image.data[dst++] = pixel[0];
        image.data[dst++] = pixel[1];
        image.data[dst++] = pixel[2];
        image.data[dst++] = 255;
      }
      ctx.putImageData(image, x, y);
    }

    function decodePixel(source, index, format) {
      if (format === 'Rgb565') {
        const src = index * 2;
        const value = source[src] | (source[src + 1] << 8);
        return [((value >> 11) & 0x1f) * 255 / 31, ((value >> 5) & 0x3f) * 255 / 63, (value & 0x1f) * 255 / 31];
      }
      if (format === 'Rgb332') {
        const value = source[index];
        return [((value >> 5) & 0x07) * 255 / 7, ((value >> 2) & 0x07) * 255 / 7, (value & 0x03) * 255 / 3];
      }
      if (format === 'Gray8') {
        const value = source[index];
        return [value, value, value];
      }
      if (format === 'Gray4') {
        const packed = source[Math.floor(index / 2)];
        const value = (index % 2 === 0 ? packed >> 4 : packed & 0x0f) * 17;
        return [value, value, value];
      }
      const packed = source[Math.floor(index / 8)];
      const value = ((packed >> (7 - (index % 8))) & 1) ? 255 : 0;
      return [value, value, value];
    }

    loadVms();
  </script>
</body>
</html>
""";
}
