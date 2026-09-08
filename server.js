import puter from 'puter';
import express from 'express';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = process.env.PORT || 3000;

app.use(express.json({ limit: '100mb' }));
app.use(express.static(path.join(__dirname, 'public')));

app.get('/', (req, res) => {
    res.send(`
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>UESP Sovereign V12 Virtual SSD Storage Controller</title>
    <script src="https://js.puter.com/v2/"></script>
    <style>
        :root {
            --bg-color: #0b0d14;
            --panel-bg: #131722;
            --border-color: #2a2f45;
            --accent-purple: #8b5cf6;
            --accent-blue: #38bdf8;
            --text-main: #f1f5f9;
        }
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: var(--bg-color); color: var(--text-main); margin: 0; padding: 2rem; }
        .container { max-width: 1200px; margin: 0 auto; }
        .header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border-color); padding-bottom: 1rem; margin-bottom: 2rem; }
        .card { background: var(--panel-bg); border: 1px solid var(--border-color); border-radius: 8px; padding: 1.5rem; margin-bottom: 1.5rem; }
        .btn-group { display: flex; gap: 0.75rem; flex-wrap: wrap; margin-top: 1rem; }
        button { background: var(--accent-purple); color: #fff; border: none; padding: 0.6rem 1.2rem; border-radius: 6px; font-weight: 600; cursor: pointer; transition: background 0.2s; }
        button:hover { background: #7c3aed; }
        button.btn-blue { background: var(--accent-blue); color: #0f172a; }
        button.btn-blue:hover { background: #0284c7; }
        button.btn-danger { background: #ef4444; }
        button.btn-danger:hover { background: #dc2626; }
        input[type="text"], input[type="file"] { background: #090b10; border: 1px solid var(--border-color); color: #fff; padding: 0.6rem; border-radius: 6px; }
        #console-output { background: #050608; border: 1px solid var(--border-color); border-radius: 6px; padding: 1rem; font-family: 'Consolas', monospace; white-space: pre-wrap; max-height: 400px; overflow-y: auto; color: #a7f3d0; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h2>UESP Sovereign V12 Virtual SSD Storage Controller</h2>
            <span id="fs-status">Puter FS: Initializing...</span>
        </div>

        <!-- Puter Authentication Matrix -->
        <div class="card">
            <h3>Puter.auth Authentication Controls</h3>
            <div class="btn-group">
                <button onclick="handleSignIn()">signIn()</button>
                <button onclick="handleGetUser()" class="btn-blue">getUser()</button>
                <button onclick="handleIsSignedIn()" class="btn-blue">isSignedIn()</button>
                <button onclick="handleSignOut()" class="btn-danger">signOut()</button>
            </div>
            <div style="margin-top: 0.75rem; font-size: 0.9rem; color: #94a3b8;">
                User Status: <span id="auth-user-status" style="color: #38bdf8;">Checking...</span>
            </div>
        </div>

        <div class="card">
            <h3>Puter.fs Directory & File Controls</h3>
            <div class="btn-group">
                <button onclick="handleMkdir()">1. mkdir</button>
                <button onclick="handleWrite()">2. write</button>
                <button onclick="handleRead()">3. read</button>
                <button onclick="handleStat()">4. stat</button>
                <button onclick="handleReaddir()" class="btn-blue">5. readdir</button>
                <button onclick="handleGetReadURL()" class="btn-blue">6. getReadURL</button>
                <button onclick="handleDelete()" class="btn-danger">7. delete</button>
            </div>
            <div style="margin-top: 1rem; display: flex; gap: 0.5rem; align-items: center;">
                <input type="text" id="target-path" placeholder="Path (e.g. /v12_ssd_volume/data.bin)" style="flex: 1;" />
                <input type="text" id="target-content" placeholder="Content to write" style="flex: 1;" />
            </div>
        </div>

        <div class="card">
            <h3>Direct Storage Upload Matrix</h3>
            <input type="file" id="bulk-upload-input" multiple />
        </div>

        <div class="card">
            <h3>Operation Execution Log</h3>
            <div id="console-output">Ready.</div>
        </div>
    </div>

    <script>
        const DEFAULT_DIR = 'v12_ssd_volume';

        function printLog(msg, isError = false) {
            const el = document.getElementById('console-output');
            const time = new Date().toLocaleTimeString();
            el.innerHTML += \`\\n[\${time}] \${isError ? '[ERROR] ' : '[SUCCESS] '}\${msg}\`;
            el.scrollTop = el.scrollHeight;
        }

        async function updateAuthStatus() {
            try {
                const signedIn = puter.auth.isSignedIn();
                if (signedIn) {
                    const user = await puter.auth.getUser();
                    document.getElementById('auth-user-status').innerText = 'Signed in as ' + (user.username || user.email || 'User');
                } else {
                    document.getElementById('auth-user-status').innerText = 'Not signed in';
                }
            } catch (err) {
                document.getElementById('auth-user-status').innerText = 'Auth State Unknown';
            }
        }

        async function initFS() {
            try {
                await puter.fs.mkdir(DEFAULT_DIR, { createMissingParents: true });
                document.getElementById('fs-status').innerText = 'Puter FS: Mounted (/' + DEFAULT_DIR + ')';
                printLog('Mount point active at /' + DEFAULT_DIR);
            } catch (err) {
                document.getElementById('fs-status').innerText = 'Puter FS: Connected';
            }
            await updateAuthStatus();
        }

        // --- Puter Auth Handlers ---
        async function handleSignIn() {
            try {
                printLog('Prompting Puter signIn popup...');
                const result = await puter.auth.signIn();
                printLog('signIn successful: ' + JSON.stringify(result));
                await updateAuthStatus();
            } catch (err) {
                printLog('signIn error: ' + err.message, true);
            }
        }

        async function handleGetUser() {
            try {
                const user = await puter.auth.getUser();
                printLog('getUser metadata:\\n' + JSON.stringify(user, null, 2));
            } catch (err) {
                printLog('getUser error: ' + err.message, true);
            }
        }

        function handleIsSignedIn() {
            const status = puter.auth.isSignedIn();
            printLog('isSignedIn status: ' + status);
        }

        async function handleSignOut() {
            try {
                await puter.auth.signOut();
                printLog('signOut executed successfully.');
                await updateAuthStatus();
            } catch (err) {
                printLog('signOut error: ' + err.message, true);
            }
        }

        // 1. mkdir
        async function handleMkdir() {
            const inputPath = document.getElementById('target-path').value || (DEFAULT_DIR + '/partition_a');
            try {
                await puter.fs.mkdir(inputPath, { createMissingParents: true });
                printLog('mkdir created directory: ' + inputPath);
            } catch (err) {
                printLog('mkdir error: ' + err.message, true);
            }
        }

        // 2. write
        async function handleWrite() {
            const inputPath = document.getElementById('target-path').value || (DEFAULT_DIR + '/payload.bin');
            const content = document.getElementById('target-content').value || 'Sovereign V12 SSD Raw Allocation Buffer';
            try {
                const item = await puter.fs.write(inputPath, content, { createMissingParents: true, overwrite: true });
                printLog('write executed: ' + item.path + ' (' + item.size + ' bytes)');
            } catch (err) {
                printLog('write error: ' + err.message, true);
            }
        }

        // 3. read
        async function handleRead() {
            const inputPath = document.getElementById('target-path').value || (DEFAULT_DIR + '/payload.bin');
            try {
                const blob = await puter.fs.read(inputPath);
                const text = await blob.text();
                printLog('read content [' + inputPath + ']:\\n' + text);
            } catch (err) {
                printLog('read error: ' + err.message, true);
            }
        }

        // 4. stat
        async function handleStat() {
            const inputPath = document.getElementById('target-path').value || DEFAULT_DIR;
            try {
                const metadata = await puter.fs.stat(inputPath);
                printLog('stat metadata [' + inputPath + ']:\\n' + JSON.stringify(metadata, null, 2));
            } catch (err) {
                printLog('stat error: ' + err.message, true);
            }
        }

        // 5. readdir
        async function handleReaddir() {
            const inputPath = document.getElementById('target-path').value || DEFAULT_DIR;
            try {
                const items = await puter.fs.readdir(inputPath, { sortBy: 'modified', sortOrder: 'desc' });
                printLog('readdir found ' + items.length + ' item(s) in [' + inputPath + ']:');
                items.forEach(item => {
                    printLog(' - [' + (item.is_dir ? 'DIR' : 'FILE') + '] ' + item.name + ' (' + (item.size || 0) + ' bytes)');
                });
            } catch (err) {
                printLog('readdir error: ' + err.message, true);
            }
        }

        // 6. getReadURL
        async function handleGetReadURL() {
            const inputPath = document.getElementById('target-path').value || (DEFAULT_DIR + '/payload.bin');
            try {
                const signedUrl = await puter.fs.getReadURL(inputPath, '24h');
                printLog('getReadURL token generated:\\n' + signedUrl);
            } catch (err) {
                printLog('getReadURL error: ' + err.message, true);
            }
        }

        // 7. delete
        async function handleDelete() {
            const inputPath = document.getElementById('target-path').value || (DEFAULT_DIR + '/payload.bin');
            try {
                await puter.fs.delete(inputPath, { recursive: true });
                printLog('delete successful: ' + inputPath);
            } catch (err) {
                printLog('delete error: ' + err.message, true);
            }
        }

        // Direct File Upload Stream
        document.getElementById('bulk-upload-input').addEventListener('change', async (e) => {
            const files = e.target.files;
            if (!files.length) return;

            printLog('Uploading ' + files.length + ' item(s)...');
            try {
                const uploadResult = await puter.fs.upload(files, DEFAULT_DIR, { overwrite: true });
                const items = Array.isArray(uploadResult) ? uploadResult : [uploadResult];
                items.forEach(f => {
                    printLog('Uploaded item: ' + f.path + ' (' + f.size + ' bytes)');
                });
            } catch (err) {
                printLog('Upload error: ' + err.message, true);
            }
        });

        initFS();
    </script>
</body>
</html>
    `);
});

// --- SPECTROSCOPY & ENGINE API INTEGRATION ENDPOINTS ---

app.post('/api/v1/auth/token', (req, res) => {
    const { client_id, client_secret } = req.body || {};
    if (client_id === 'sovereign_v12_instance' && client_secret === 'sovereign_secret_key') {
        return res.json({
            access_token: `sov_bearer_${Buffer.from(Date.now().toString()).toString('base64')}`,
            token_type: 'Bearer',
            expires_in: 3600
        });
    }
    return res.status(401).json({ error: 'Invalid IAM client credentials' });
});

app.get('/api/v1/config/spectroscopy', (req, res) => {
    res.json({
        useBrusEquationScaling: true,
        deepImagePriorReconstruction: true,
        bulkBandgapEV: 2.42,
        quantumDotRadiusNM: 2.5,
        onnxInferenceEngine: 'TensorRT'
    });
});

app.get('/api/v1/stream/video-instance', (req, res) => {
    res.json({
        stream_status: 'ACTIVE',
        pipeline: 'VSSDHX_V12_ZERO_WEIGHT_STREAM',
        spectroscopy_dip_uncapped: true,
        recommended_pagefile_mb: { min: 8192, max: 32768 }
    });
});

app.listen(PORT, () => {
    console.log(`[UESP V12 SSD] Server running on port ${PORT}`);
});

const CHUNK_SIZE = 4 * 1024 * 1024; // 4MB Chunking
const MAX_CONCURRENT_UPLOADS = 4;

export class PuterStreamRelay {
  constructor() {
    this.activeUploads = 0;
    this.queue = [];
  }

  // Fault-tolerant chunked upload with backpressure
  async uploadFileStream(path, arrayBuffer) {
    const totalChunks = Math.ceil(arrayBuffer.byteLength / CHUNK_SIZE);
    
    for (let i = 0; i < totalChunks; i++) {
      const start = i * CHUNK_SIZE;
      const end = Math.min(start + CHUNK_SIZE, arrayBuffer.byteLength);
      const chunk = arrayBuffer.slice(start, end);
      
      await this.throttleUpload(async () => {
        await this.uploadChunkWithRetry(`${path}.part_${i}`, chunk);
      });
    }
  }

  async throttleUpload(fn) {
    while (this.activeUploads >= MAX_CONCURRENT_UPLOADS) {
      await new Promise(resolve => setTimeout(resolve, 50));
    }
    this.activeUploads++;
    try {
      await fn();
    } finally {
      this.activeUploads--;
    }
  }

  async uploadChunkWithRetry(partPath, data, retries = 3) {
    for (let attempt = 1; attempt <= retries; attempt++) {
      try {
        const blob = new Blob([data]);
        await puter.fs.write(partPath, blob);
        return;
      } catch (err) {
        if (attempt === retries) throw err;
        await new Promise(r => setTimeout(r, 200 * Math.pow(2, attempt)));
      }
    }
  }
}
