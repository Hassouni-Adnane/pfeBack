// routes/contact.js
const express = require('express');
const router = express.Router();

// ⚠️ Assure-toi que le nom correspond bien à ton fichier: models/contactmessage.js
const ContactMessage = require('../models/ContactMessage');

// (Optionnel) Mailer via nodemailer, seulement si les variables SMTP existent
let mailer = null;
if (process.env.SMTP_HOST && process.env.SMTP_USER && process.env.SMTP_PASS) {
  const nodemailer = require('nodemailer');
  mailer = nodemailer.createTransport({
    host: process.env.SMTP_HOST,
    port: Number(process.env.SMTP_PORT || 587),
    secure: String(process.env.SMTP_SECURE || 'false').toLowerCase() === 'true', // true si port 465
    auth: { user: process.env.SMTP_USER, pass: process.env.SMTP_PASS },
  });
}

const isEmail = (v) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);

// POST /api/contact
router.post('/', async (req, res) => {
  try {
    let { name, email, subject, message } = req.body || {};
    name = String(name || '').trim();
    email = String(email || '').trim();
    subject = String(subject || '').trim();
    message = String(message || '').trim();

    if (!name || !email || !subject || !message) {
      return res.status(400).json({ message: 'Tous les champs sont requis.' });
    }
    if (!isEmail(email)) {
      return res.status(400).json({ message: 'Email invalide.' });
    }
    if (subject.length > 200) subject = subject.slice(0, 200);
    if (message.length > 5000) message = message.slice(0, 5000);

    // 1) Enregistrement en base
    const doc = await ContactMessage.create({ name, email, subject, message });

    // 2) Envoi d’email (si SMTP configuré)
    if (mailer) {
      const to = process.env.CONTACT_TO || process.env.SMTP_USER;
      await mailer.sendMail({
        from: `"${process.env.SMTP_FROM_NAME || 'Digital Signature'}" <${process.env.SMTP_FROM_EMAIL || process.env.SMTP_USER}>`,
        to,
        replyTo: `${name} <${email}>`,
        subject: `[Contact] ${subject}`,
        text: `Nom: ${name}\nEmail: ${email}\n\n${message}`,
        html: `<p><b>Nom:</b> ${name}</p><p><b>Email:</b> ${email}</p><p>${message.replace(/\n/g, '<br>')}</p>`,
      });
    }

    return res.status(201).json({ ok: true, id: doc.id });
  } catch (err) {
    console.error('Contact error:', err);
    return res.status(500).json({ message: 'Erreur serveur' });
  }
});

// (Optionnel) petite route santé
router.get('/health', (_req, res) => res.json({ ok: true }));

module.exports = router;
