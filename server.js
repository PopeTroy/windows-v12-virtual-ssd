const { puter } = require('@heyputer/puter.js');
const express = require('express');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;

// High payload limit for metadata overhead
app.use(express.json({ limit: '100mb' }));

console.log("\x1b[35m=========================================================================");
console.log("   UESP SOVEREIGN v6.0.0 - PUTER.JS SERVERLESS BACKUP DAEMON             ");
console.log("=========================================================================\x1b[0m");

/**
 * Ensures the target directory exists inside the Puter Cloud File System.
 */
async function ensurePuterDirectory(dirPath) {
    try {
        await puter.fs.mkdir(dirPath, { recursive: true });
    } catch (err) {
        // Ignore error if directory already exists
        if (!err.message?.includes('already exists')) {
            console.warn(`\x1b[33m[!] Puter FS Directory Warning: ${err.message}\x1b[0m`);
        }
    }
}

app.post('/stream-to-bubble', async (req, res) => {
    const { filename, filePath } = req.body;

    if (!filename || !filePath) {
        return res.status(400).json({ 
            status: 'FAILED', 
            error: 'Missing required body parameters: filename and filePath.' 
        });
    }

    try {
        const resolvedPath = path.resolve(filePath);

        // 1. Check local file existence
        if (!fs.existsSync(resolvedPath)) {
            console.error(`\x1b[31m[!] Local File Allocation Missing: ${resolvedPath}\x1b[0m`);
            return res.status(404).json({ status: 'FAILED', error: 'File allocation missing.' });
        }

        // 2. Ensure destination vault directory exists in Puter FS
        const vaultDir = 'sovereign-vault';
        await ensurePuterDirectory(vaultDir);

        // 3. Read file stream/buffer and write directly to Puter Vault
        const fileBuffer = fs.readFileSync(resolvedPath);
        const targetPuterPath = `${vaultDir}/${filename}`;

        await puter.fs.write(targetPuterPath, fileBuffer);

        console.log(`\x1b[32m[✓] Puter.js Backup Success: ${filename} -> ${targetPuterPath}\x1b[0m`);
        
        return res.status(200).json({ 
            status: 'SUCCESS',
            filename,
            puterPath: targetPuterPath,
            sizeBytes: fileBuffer.length
        });

    } catch (error) {
        console.error(`\x1b[31m[CRITICAL] Backup Daemon Failure: ${error.message}\x1b[0m`);
        return res.status(500).json({ status: 'ERROR', error: error.message });
    }
});

app.listen(PORT, () => {
    console.log(`\x1b[36m[✓] Puter.js Bridge Active on port ${PORT}\x1b[0m`);
});
