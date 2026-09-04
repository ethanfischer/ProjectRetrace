// One socket for the whole page: the game only ever talks to one relay at a time.
// Callbacks go through SendMessage to the GameObject named at connect time.
mergeInto(LibraryManager.library, {
  RetraceWs_Connect: function (urlPtr, goPtr) {
    var url = UTF8ToString(urlPtr);
    var go = UTF8ToString(goPtr);
    if (window.__retraceWs) {
      try { window.__retraceWs.onclose = null; window.__retraceWs.close(); } catch (e) {}
    }
    var ws;
    try {
      ws = new WebSocket(url);
    } catch (e) {
      SendMessage(go, 'OnWsClose', String(e));
      return;
    }
    window.__retraceWs = ws;
    ws.onopen = function () { SendMessage(go, 'OnWsOpen', ''); };
    ws.onmessage = function (e) { SendMessage(go, 'OnWsMessage', e.data); };
    ws.onclose = function (e) { SendMessage(go, 'OnWsClose', 'closed ' + e.code); };
    ws.onerror = function () {};
  },
  RetraceWs_Send: function (ptr) {
    var ws = window.__retraceWs;
    if (ws && ws.readyState === 1) ws.send(UTF8ToString(ptr));
  },
  RetraceWs_Close: function () {
    var ws = window.__retraceWs;
    if (ws) ws.close();
  },
  RetraceWs_State: function () {
    var ws = window.__retraceWs;
    return ws ? ws.readyState : 3;
  }
});
