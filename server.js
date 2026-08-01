const { puter } = require('@heyputer/puter.js');
const express = require('express');
const fs = require('fs');

const app = express();
const port = 3000;

app.use(express.json({ limit: '100mb' }));

console.log("\x1b[35m=========================================================================");
console.log("   UESP SOVEREIGN v6.0.0 - PUTER.JS SERVERLESS BACKUP DAEMON             ");
console.log("=========================================================================\x1b[0m");

app.post('/stream-to-bubble', async (req, res) => {
    const { filename, filePath } = req.body;

    if (!filename || !filePath) {
        return res.status(400).json({ error: 'Missing parameters.' });
    }

    try {
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ error: 'File allocation missing.' });
        }
        const fileBuffer = fs.readFileSync(filePath);
        await puter.fs.write(`sovereign-vault/${filename}`, fileBuffer);

        console.log(`\x1b[32m[✓] Puter.js Backup Success: ${filename}\x1b[0m`);
        return res.status(200).json({ status: 'SUCCESS' });
    } catch (error) {
        return res.status(500).json({ error: error.message });
    }
});

app.listen(port, () => {
    console.log(`\x1b[36m[✓] Puter.js Bridge active on port ${port}\x1b[0m`);
});
