// routes/documents.js
const express = require('express');
const router = express.Router();
const Document = require('../models/Document');
const User = require('../models/User'); // only if you later want to validate uploaderUserId exists

// Shared secret middleware so only your .NET backend can call this
const requireWebhookSecret = (req, res, next) => {
  // 👇 fallback matches the .NET side
  const expected = process.env.DOCUMENTS_WEBHOOK_SECRET || 'supersecret123';
  const got = req.get('x-api-key') || ''; // header name is case-insensitive
  if (got !== expected) {
    return res.status(401).json({ message: 'Unauthorized' });
  }
  next();
};


// Reuse the numeric +1 id pattern like routes/users.js
const getNextId = async () => {
  const last = await Document.findOne().sort({ id: -1 }).lean();
  return (last?.id ?? 0) + 1;
};

/**
 * POST /api/documents
 * Body JSON:
 * {
 *   "signNowDocumentId": "string",  // required
 *   "workflow": "parallel"|"sequential", // required
 *   "uploadedAt": "2025-09-21T15:20:00Z", // optional; defaults to now
 *   "originalName": "contract.pdf",
 *   "contentType": "application/pdf",
 *   "sizeBytes": 12345,
 *   "embeddedSendingUrl": "https://...",
 *   "uploaderUserId": "abc123", // optional
 *   "meta": { ... } // optional
 * }
 */
router.post('/', requireWebhookSecret, async (req, res) => {
  try {
    const {
      signNowDocumentId,
      workflow,
      uploadedAt,
      originalName,
      contentType,
      sizeBytes,
      embeddedSendingUrl,
      uploaderUserId,
      meta
    } = req.body || {};

    if (!signNowDocumentId) {
      return res.status(400).json({ message: 'signNowDocumentId is required' });
    }
    if (!['parallel', 'sequential'].includes(String(workflow).toLowerCase())) {
      return res.status(400).json({ message: 'workflow must be "parallel" or "sequential"' });
    }

    const id = await getNextId();

    const doc = await Document.create({
      id,
      signNowDocumentId,
      workflow: String(workflow).toLowerCase(),
      uploadedAt: uploadedAt ? new Date(uploadedAt) : new Date(),
      originalName,
      contentType,
      sizeBytes,
      embeddedSendingUrl,
      uploaderUserId,
      meta: meta || {}
    });

    return res.status(201).json({
      message: 'Document logged',
      id: doc.id,
      mongo_id: doc._id,
      signNowDocumentId: doc.signNowDocumentId,
      workflow: doc.workflow,
      uploadedAt: doc.uploadedAt
    });
  } catch (err) {
    console.error('Create document error:', err);
    if (err?.code === 11000) {
      return res.status(409).json({ message: 'Duplicate signNowDocumentId' });
    }
    return res.status(500).json({ message: 'Erreur serveur' });
  }
});

/** (Optional) simple list */
router.get('/', async (_req, res) => {
  const items = await Document.find().select('-__v').sort({ uploadedAt: -1 }).limit(100);
  res.json(items);
});

module.exports = router;
