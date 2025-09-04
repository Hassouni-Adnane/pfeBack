// routes/users.js
const express = require("express");
const bcrypt = require("bcrypt");
const router = express.Router();
const User = require("../models/User");

/* ---------- Helpers ---------- */
const stripPassword = (doc) => {
  const asObj = doc.toObject ? doc.toObject() : doc;
  const { password, ...rest } = asObj;
  return rest;
};
const normalizeEmail = (email) => String(email || "").trim().toLowerCase();
const getNextId = async () => {
  const last = await User.findOne().sort({ id: -1 }).lean();
  return (last?.id ?? 0) + 1;
};

/* =========================================================
 * POST /api/users/register
 * -> Inscription utilisateur (status: Pending par défaut)
 * ======================================================= */
router.post("/register", async (req, res) => {
  try {
    let { name, email, jobTitle, password } = req.body || {};
    if (!name || !email || !password) {
      return res.status(400).json({ message: "Nom, email et mot de passe sont requis." });
    }

    email = normalizeEmail(email);

    const exists = await User.findOne({ email });
    if (exists) return res.status(409).json({ message: "Cet email est déjà utilisé." });

    const id = await getNextId();

    // ⚠️ NE PAS hasher ici : le pre('save') du modèle User s'en charge
    const user = new User({
      id,
      name,
      email,
      jobTitle: jobTitle || "",
      password,            // sera hashé par le pre('save') du modèle
      status: "Pending",   // ou "Approved" si tu veux autoriser la connexion directe
      role: "user",
    });

    const saved = await user.save();
    return res.status(201).json(stripPassword(saved));
  } catch (err) {
    console.error("Register error:", err);
    return res.status(500).json({ message: "Erreur serveur" });
  }
});

/* =========================================================
 * POST /api/users
 * -> Création par un admin
 * ======================================================= */
router.post("/", async (req, res) => {
  try {
    let { name, email, jobTitle, password, role, status } = req.body || {};
    if (!name || !email || !password) {
      return res.status(400).json({ message: "Nom, email et mot de passe sont requis." });
    }

    email = normalizeEmail(email);

    const exists = await User.findOne({ email });
    if (exists) return res.status(409).json({ message: "Cet email est déjà utilisé." });

    const id = await getNextId();

    const user = new User({
      id,
      name,
      email,
      jobTitle: jobTitle || "",
      password,                     // pre('save') hashe
      role: role || "user",
      status: status || "Pending",
    });

    const saved = await user.save();
    return res.status(201).json(stripPassword(saved));
  } catch (err) {
    console.error("Create user error:", err);
    return res.status(500).json({ message: "Erreur serveur" });
  }
});

/* =========================================================
 * GET /api/users
 * -> Liste (sans password)
 * ======================================================= */
router.get("/", async (_req, res) => {
  try {
    const users = await User.find().select("-password");
    return res.json(users);
  } catch (err) {
    console.error("List users error:", err);
    return res.status(500).json({ message: "Erreur serveur" });
  }
});

/* =========================================================
 * GET /api/users/:id
 * -> Détail (sans password)
 * ======================================================= */
router.get("/:id", async (req, res) => {
  try {
    const u = await User.findOne({ id: Number(req.params.id) }).select("-password");
    if (!u) return res.status(404).json({ message: "Utilisateur introuvable" });
    return res.json(u);
  } catch (err) {
    console.error("Get user error:", err);
    return res.status(500).json({ message: "Erreur serveur" });
  }
});

/* =========================================================
 * PUT /api/users/:id
 * -> Mise à jour (rehash si password modifié)
 *   (findOneAndUpdate ne déclenche PAS pre('save'))
 * ======================================================= */
router.put("/:id", async (req, res) => {
  try {
    const updates = { ...req.body };

    if (updates.email) updates.email = normalizeEmail(updates.email);

    if (updates.password) {
      updates.password = await bcrypt.hash(String(updates.password), 10);
    }

    const updated = await User.findOneAndUpdate(
      { id: Number(req.params.id) },
      { $set: updates },
      { new: true }
    );

    if (!updated) return res.status(404).json({ message: "Utilisateur introuvable" });
    return res.json(stripPassword(updated));
  } catch (err) {
    console.error("Update user error:", err);
    return res.status(500).json({ message: "Erreur serveur" });
  }
});

/* =========================================================
 * DELETE /api/users/:id
 * ======================================================= */
router.delete("/:id", async (req, res) => {
  try {
    const deleted = await User.findOneAndDelete({ id: Number(req.params.id) });
    if (!deleted) return res.status(404).json({ message: "Utilisateur introuvable" });
    return res.json({ message: "Utilisateur supprimé" });
  } catch (err) {
    console.error("Delete user error:", err);
    return res.status(500).json({ message: "Erreur serveur" });
  }
});

module.exports = router;
