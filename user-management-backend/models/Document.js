// models/Document.js
const mongoose = require('mongoose');

const documentSchema = new mongoose.Schema({
  id:                 { type: Number, required: true, unique: true }, // numeric, like User.id pattern
  signNowDocumentId:  { type: String, required: true, unique: true },
  workflow:           { type: String, enum: ['parallel', 'sequential'], required: true },
  uploadedAt:         { type: Date, default: Date.now }, // UTC now; or accept client-provided date
  originalName:       { type: String },
  contentType:        { type: String, default: 'application/pdf' },
  sizeBytes:          { type: Number },
  embeddedSendingUrl: { type: String },
  uploaderUserId:     { type: String },
  meta:               { type: mongoose.Schema.Types.Mixed, default: {} },
}, { timestamps: true });

module.exports = mongoose.model('Document', documentSchema);
