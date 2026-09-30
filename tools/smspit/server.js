const http = require('http');
const url = require('url');

const PORT = process.env.PORT || 8026;
const messages = [];

const server = http.createServer((req, res) => {
  const parsedUrl = url.parse(req.url, true);
  const path = parsedUrl.pathname;

  // 1. 三竹相容發送端點：POST /api/mtk/SmSend
  if (path === '/api/mtk/SmSend') {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      let params = parsedUrl.query || {};
      if (body) {
        try {
          const formParams = new URLSearchParams(body);
          for (const [key, value] of formParams.entries()) {
            params[key] = value;
          }
        } catch (e) {
          // ignore
        }
      }

      const dstaddr = params.dstaddr || '';
      const smbody = params.smbody || '';
      const msgid = Date.now().toString() + Math.floor(Math.random() * 1000);

      const msg = {
        id: msgid,
        to: dstaddr,
        message: smbody,
        username: params.username || '',
        createdAt: new Date().toISOString()
      };

      messages.unshift(msg);
      if (messages.length > 500) messages.pop();

      console.log(`[Smspit] 攔截到發送至 ${dstaddr} 的簡訊：${smbody}`);

      // 三竹標準成功回應格式
      res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end(`[#001]\nmsgid=${msgid}\nstatuscode=1\n`);
    });
    return;
  }

  // 2. 測試與自動化 REST API：/api/v1/messages
  if (path === '/api/v1/messages') {
    if (req.method === 'DELETE') {
      messages.length = 0;
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ success: true }));
      return;
    }

    const toQuery = parsedUrl.query.to;
    let list = messages;
    if (toQuery) {
      list = messages.filter(m => m.to === toQuery || m.to.includes(toQuery));
    }

    res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
    res.end(JSON.stringify({
      total: list.length,
      messages: list
    }));
    return;
  }

  // 3. 簡易可視化 Web UI：GET /
  if (path === '/' || path === '/index.html') {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    const rows = messages.map(m => `
      <tr>
        <td style="padding: 8px; border: 1px solid #ddd;">${m.createdAt}</td>
        <td style="padding: 8px; border: 1px solid #ddd;"><strong>${m.to}</strong></td>
        <td style="padding: 8px; border: 1px solid #ddd;">${m.message}</td>
        <td style="padding: 8px; border: 1px solid #ddd; color: #888;">${m.id}</td>
      </tr>
    `).join('');

    res.end(`<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>Smspit - 虛擬簡訊攔截器</title>
  <meta http-equiv="refresh" content="5">
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; margin: 2rem; background: #f8fafc; color: #1e293b; }
    h1 { margin-bottom: 0.5rem; }
    table { width: 100%; border-collapse: collapse; background: #fff; margin-top: 1rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); border-radius: 4px; overflow: hidden; }
    th { background: #3b82f6; color: #fff; padding: 10px; text-align: left; }
    .badge { background: #10b981; color: white; padding: 2px 8px; border-radius: 999px; font-size: 0.8rem; }
  </style>
</head>
<body>
  <h1>📱 Smspit 虛擬簡訊伺服器 <span class="badge">已接收 ${messages.length} 則</span></h1>
  <p>攔截所有三竹簡訊請求，不向外投遞至真實手機。本頁面每 5 秒自動重新整理。</p>
  <table>
    <thead>
      <tr>
        <th>時間 (UTC)</th>
        <th>受訊門號 (dstaddr)</th>
        <th>簡訊內容 (smbody)</th>
        <th>簡訊 ID</th>
      </tr>
    </thead>
    <tbody>
      ${rows || '<tr><td colspan="4" style="padding: 16px; text-align: center; color: #64748b;">目前尚未收到任何簡訊</td></tr>'}
    </tbody>
  </table>
</body>
</html>`);
    return;
  }

  if (path === '/health') {
    res.writeHead(200, { 'Content-Type': 'text/plain' });
    res.end('OK');
    return;
  }

  res.writeHead(404, { 'Content-Type': 'text/plain' });
  res.end('Not Found');
});

server.listen(PORT, '0.0.0.0', () => {
  console.log(`[Smspit] 虛擬簡訊伺服器已啟動於連接埠 ${PORT}`);
});
