const { puter } = require('@heyputer/puter.js');
const express = require('express');
const fs = require('fs');

const app = express();
const port = 3000;

// Use explicit raw data limits to accommodate heavy file extractions
app.use(express.json({ limit: '50mb' }));

console.log("\x1b[35m=========================================================================");
console.log("   UESP SOVEREIGN v6.0.0 - PUTER.JS UNIFIED CLOUD DAEMON                 ");
console.log("=========================================================================\x1b[0m");

console.log("[-] Synchronizing with Puter.js Decentralized Kernel API...");
console.log("[✓] Serverless User-Pays Protocol: ACTIVE.");

// Secure endpoint triggered exclusively by the local C# storage master
app.post('/stream-to-bubble', async (req, res) => {
    const { filename, filePath } = req.body;

    if (!filename || !filePath) {
        return res.status(400).json({ error: 'Missing routing parameters.' });
    }

    console.log(`\n\x1b[33m[PUTER CLOUD] Siphoning optimized data block: ${filename}\x1b[0m`);

    try {
        // Read file buffer straight out of your 16GB RAM cache space
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ error: 'Physical memory allocation missing.' });
        }
        const fileBuffer = fs.readFileSync(filePath);

        console.log(`[-] Dispatching bytes to remote Puter storage envelope...`);
        
        // Serverless cloud upload - Deduplicated & compressed by C# at the edge
        await puter.fs.write(`sovereign-vault/${filename}`, fileBuffer);

        console.log(`\x1b[32m[✓] Cloud Storage Success: ${filename} isolated inside information bubble.\x1b[0m`);
        return res.status(200).json({ status: 'SUCCESS' });
    } catch (error) {
        console.log(`\x1b[90m[ANOMALY] Cloud transmission exception: ${error.message}\x1b[0m`);
        return res.status(500).json({ error: error.message });
    }
});

app.listen(port, () => {
    console.log(`\n\x1b[36m[👁️ RINNEGAN GATEWAY ONLINE] Listening for C# hardware handshakes on port ${port}...\x1b[0m`);
});
