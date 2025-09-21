// routes/login.js
const express = require("express");
const bcrypt = require("bcrypt");
const router = express.Router();
const User = require("../models/User");

const normalizeEmail = (email) => String(email || "").trim().toLowerCase();

router.post("/", async (req, res) => {
  const email = normalizeEmail(req.body.email);
  const { password } = req.body;

  try {
    const user = await User.findOne({ email });
    if (!user) return res.status(404).json({ message: "Utilisateur non trouvé" });

    // (Option) bloquer les comptes non approuvés
    // if (user.status !== "Approved") {
    //   return res.status(403).json({ message: "Compte en attente d'approbation" });
    // }

    const ok = await bcrypt.compare(String(password), user.password);
    if (!ok) return res.status(401).json({ message: "Mot de passe incorrect" });

    return res.status(200).json({
      message: "Authentification réussie",
      user: {
        id: user.id,
        email: user.email,
        role: user.role,
        status: user.status,
        name: user.name,
        jobTitle: user.jobTitle,
      },
      // token: "..."  // ajoute un JWT ici si besoin
    });
  } catch (err) {
    console.error("Login error:", err);
    return res.status(500).json({ message: "Erreur serveur" });
  }
});

module.exports = router;
