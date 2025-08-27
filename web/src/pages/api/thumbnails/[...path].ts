/**
 * Thumbnail serving API for stage thumbnails
 * Serves SVG thumbnails from public/stage-thumbnails directory
 */

import { NextApiRequest, NextApiResponse } from 'next';
import fs from 'fs';
import path from 'path';

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  if (req.method !== 'GET') {
    res.setHeader('Allow', ['GET']);
    return res.status(405).json({ error: 'Method not allowed' });
  }

  try {
    const { path: filePath } = req.query;
    
    if (!filePath || !Array.isArray(filePath)) {
      return res.status(400).json({ error: 'Invalid file path' });
    }

    // Reconstruct the filename
    const fileName = filePath.join('/');
    
    // Security: Only allow specific file patterns
    if (!fileName.match(/^stage-\d+.*\.(svg|png|jpg|jpeg)$/i)) {
      return res.status(404).json({ error: 'File not found' });
    }

    // Construct absolute file path using environment variable
    const { env } = await import('@/lib/env');
    const storageDir = path.isAbsolute(env.THUMBNAIL_STORAGE_DIR) 
      ? env.THUMBNAIL_STORAGE_DIR 
      : path.join(process.cwd(), env.THUMBNAIL_STORAGE_DIR);
    const absolutePath = path.join(storageDir, fileName);

    // Security: Prevent directory traversal
    if (!absolutePath.startsWith(storageDir)) {
      return res.status(404).json({ error: 'File not found' });
    }

    // Check if file exists
    if (!fs.existsSync(absolutePath)) {
      return res.status(404).json({ error: 'Thumbnail not found' });
    }

    // Read file stats
    const stats = fs.statSync(absolutePath);
    const fileExtension = path.extname(fileName).toLowerCase();

    // Set appropriate headers
    const mimeTypes: Record<string, string> = {
      '.svg': 'image/svg+xml',
      '.png': 'image/png',
      '.jpg': 'image/jpeg',
      '.jpeg': 'image/jpeg'
    };

    const mimeType = mimeTypes[fileExtension] || 'application/octet-stream';

    // Cache headers for performance
    res.setHeader('Content-Type', mimeType);
    res.setHeader('Content-Length', stats.size);
    res.setHeader('Cache-Control', 'public, max-age=86400'); // 1 day cache
    res.setHeader('ETag', `"${stats.mtime.getTime()}-${stats.size}"`);
    res.setHeader('Last-Modified', stats.mtime.toUTCString());

    // CORS headers for cross-origin access
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET');
    res.setHeader('Access-Control-Max-Age', '86400');

    // Handle conditional requests (304 Not Modified)
    const ifNoneMatch = req.headers['if-none-match'];
    const ifModifiedSince = req.headers['if-modified-since'];
    const etag = `"${stats.mtime.getTime()}-${stats.size}"`;

    if (ifNoneMatch === etag || 
        (ifModifiedSince && new Date(ifModifiedSince) >= stats.mtime)) {
      return res.status(304).end();
    }

    // Stream the file
    const readStream = fs.createReadStream(absolutePath);
    readStream.pipe(res);

    readStream.on('error', (error) => {
      console.error('Error streaming thumbnail:', error);
      if (!res.headersSent) {
        res.status(500).json({ error: 'Error reading file' });
      }
    });

  } catch (error) {
    console.error('Thumbnail API error:', error);
    res.status(500).json({ error: 'Internal server error' });
  }
}

// Disable body parsing for streaming
export const config = {
  api: {
    bodyParser: false,
  },
};