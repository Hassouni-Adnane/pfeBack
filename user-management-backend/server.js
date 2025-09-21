// server.js
const express = require("express");
const mongoose = require("mongoose");
const dotenv = require("dotenv");
const cors = require("cors");
// server.js





// Routes
const userRoutes   = require("./routes/users");
const loginRoute   = require("./routes/login");
const contactRoute = require("./routes/contact");
const documentRoutes = require("./routes/documents");
// Modèle pour routes debug
const User = require("./models/User");

dotenv.config();

const app = express();

// Middlewares
app.use(express.json());
app.use(cors({
  origin: "http://localhost:3000",
  credentials: true,
}));

// Connexion MongoDB (les options useNewUrlParser/useUnifiedTopology ne sont plus nécessaires)
mongoose.connect(process.env.MONGO_URI);

mongoose.connection.on("connected", () => {
  console.log("✅ Mongo connecté");
  console.log("DB name:", mongoose.connection.name);
  console.log("Host:", mongoose.connection.host);
});
mongoose.connection.on("error", (err) => {
  console.error("❌ Mongo error:", err);
});

// Routes API
app.use("/api/users", userRoutes);
app.use("/api/login", loginRoute);
app.use("/api/contact", contactRoute);         // ✅ ICI, avant le 404
app.use("/api/documents", documentRoutes);
// --- DEBUG ---
app.get("/api/debug/health", (req, res) => {
  res.json({
    mongoState: mongoose.connection.readyState, // 1 = connecté
    dbName: mongoose.connection.name,
    host: mongoose.connection.host,
  });
});

app.get("/api/debug/users-count", async (req, res) => {
  const count = await User.countDocuments({});
  res.json({ count });
});

// Route de test
app.get("/", (_req, res) => res.json({ message: "API en ligne 🚀" }));

// 404 (catch-all) — ⚠️ toujours APRÈS toutes les routes
app.use((_req, res) => res.status(404).json({ message: "Route non trouvée" }));

// Handler d'erreurs — tout à la fin
app.use((err, _req, res, _next) => {
  console.error("Erreur serveur :", err);
  res.status(500).json({ message: "Erreur serveur" });
});

// Lancement
const PORT = process.env.PORT || 5000;
app.listen(PORT, () => {
  console.log(`🚀 Serveur démarré sur le port ${PORT}`);
});
