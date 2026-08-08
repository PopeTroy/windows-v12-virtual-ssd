const { puter } = require('@heyputer/puter.js');
const express = require('express');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;

// High payload limit to handle SSD block transfers up to 100MB
app.use(express.json({ limit: '100mb' }));

console.log("\x1b[35m=========================================================================");
console.log("   SNAKE SAGE ENGINE v1.0.0 - PUTER.JS VIRTUAL SSD BRIDGE DAEMON         ");
console.log("=========================================================================\x1b[0m");

const VIRTUAL_VAULT_ROOT = 'sovereign-vault';

/**
 * Ensures parent directories exist in Puter FS prior to file operations
 */
async function ensureVaultDirectory(targetDir = VIRTUAL_VAULT_ROOT) {
    try {
        await puter.fs.mkdir(targetDir, { createMissingParents: true, overwrite: false });
    } catch (err) {
        // Ignore error if directory already exists
    }
}

// =========================================================================
// 1. WRITE OPERATION (Native puter.fs.write with createMissingParents)
// =========================================================================
app.post('/ssd/write', async (req, res) => {
    const { filename, filePath, content } = req.body;

    if (!filename) {
        return res.status(400).json({ status: 'FAILED', error: 'Missing filename parameter.' });
    }

    try {
        await ensureVaultDirectory(VIRTUAL_VAULT_ROOT);
        const destinationPath = `${VIRTUAL_VAULT_ROOT}/${filename}`;

        let payloadData = content;

        // If a local host file path was supplied, read its raw buffer
        if (filePath) {
            const resolvedPath = path.resolve(filePath);
            if (!fs.existsSync(resolvedPath)) {
                return res.status(404).json({ status: 'FAILED', error: `Host file missing: ${resolvedPath}` });
            }
            payloadData = fs.readFileSync(resolvedPath);
        }

        if (payloadData === undefined || payloadData === null) {
            return res.status(400).json({ status: 'FAILED', error: 'No payload data or valid filePath provided.' });
        }

        // Execute write with mandatory createMissingParents and overwrite enabled
        const fsItem = await puter.fs.write(destinationPath, payloadData, {
            overwrite: true,
            createMissingParents: true
        });

        console.log(`\x1b[32m[✓ SSD WRITE SUCCESS] Path: ${fsItem.path} | Size: ${fsItem.size || payloadData.length} Bytes\x1b[0m`);
        
        return res.status(200).json({
            status: 'SUCCESS',
            path: fsItem.path,
            name: fsItem.name,
            size: fsItem.size || payloadData.length
        });

    } catch (error) {
        console.error(`\x1b[31m[CRITICAL SSD WRITE ERROR] ${error.message}\x1b[0m`);
        return res.status(500).json({ status: 'ERROR', error: error.message });
    }
});

// =========================================================================
// 2. READ OPERATION (Native puter.fs.read)
// =========================================================================
app.get('/ssd/read/:filename', async (req, res) => {
    const { filename } = req.params;
    const targetPath = `${VIRTUAL_VAULT_ROOT}/${filename}`;

    try {
        const blob = await puter.fs.read(targetPath);
        const arrayBuffer = await blob.arrayBuffer();
        const buffer = Buffer.from(arrayBuffer);

        console.log(`\x1b[36m[✓ SSD READ SUCCESS] Path: ${targetPath} | Read Bytes: ${buffer.length}\x1b[0m`);
        
        res.setHeader('Content-Type', 'application/octet-stream');
        res.setHeader('Content-Disposition', `attachment; filename="${filename}"`);
        return res.send(buffer);

    } catch (error) {
        console.error(`\x1b[31m[CRITICAL SSD READ ERROR] ${error.message}\x1b[0m`);
        return res.status(404).json({ status: 'ERROR', error: `File unreadable or missing: ${error.message}` });
    }
});

// =========================================================================
// 3. DELETE OPERATION (Native puter.fs.delete)
// =========================================================================
app.delete('/ssd/delete/:filename', async (req, res) => {
    const { filename } = req.params;
    const targetPath = `${VIRTUAL_VAULT_ROOT}/${filename}`;

    try {
        await puter.fs.delete(targetPath, { recursive: true });
        console.log(`\x1b[33m[✓ SSD DELETE SUCCESS] Path: ${targetPath}\x1b[0m`);
        return res.status(200).json({ status: 'SUCCESS', deletedPath: targetPath });
    } catch (error) {
        console.error(`\x1b[31m[CRITICAL SSD DELETE ERROR] ${error.message}\x1b[0m`);
        return res.status(500).json({ status: 'ERROR', error: error.message });
    }
});

// =========================================================================
// 4. STAT / BLOCK INFO (Native puter.fs.stat)
// =========================================================================
app.get('/ssd/stat/:filename', async (req, res) => {
    const { filename } = req.params;
    const targetPath = `${VIRTUAL_VAULT_ROOT}/${filename}`;

    try {
        const stat = await puter.fs.stat(targetPath, { returnSize: true });
        return res.status(200).json({
            status: 'SUCCESS',
            name: stat.name,
            path: stat.path,
            size: stat.size,
            created: stat.created
        });
    } catch (error) {
        return res.status(404).json({ status: 'ERROR', error: error.message });
    }
});

// =========================================================================
// 5. DIRECTORY LISTING / VSSD DRIVE CONTENT (Native puter.fs.readdir)
// =========================================================================
app.get('/ssd/list', async (req, res) => {
    try {
        await ensureVaultDirectory(VIRTUAL_VAULT_ROOT);
        const items = await puter.fs.readdir(VIRTUAL_VAULT_ROOT);
        
        return res.status(200).json({
            status: 'SUCCESS',
            vault: VIRTUAL_VAULT_ROOT,
            count: items.length,
            files: items.map(i => ({ name: i.name, path: i.path, size: i.size }))
        });
    } catch (error) {
        return res.status(500).json({ status: 'ERROR', error: error.message });
    }
});

// Backward compatibility endpoint for legacy calls
app.post('/stream-to-bubble', async (req, res) => {
    req.url = '/ssd/write';
    return app._router.handle(req, res);
});

app.listen(PORT, () => {
    console.log(`\x1b[36m[✓] Snake Sage VSSD Bridge active on http://localhost:${PORT}\x1b[0m`);
});
