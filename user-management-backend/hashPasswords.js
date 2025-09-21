const mongoose = require('mongoose');
const bcrypt = require('bcrypt');
const User = require('./models/User');
require('dotenv').config();

const hashUnhashedPasswords = async () => {
  try {
    await mongoose.connect(process.env.MONGO_URI, {
      useNewUrlParser: true,
      useUnifiedTopology: true,
    });

    const users = await User.find();

    for (const user of users) {
      // Si déjà hashé, passe
      if (user.password.startsWith('$2b$')) continue;

      const hashedPassword = await bcrypt.hash(user.password, 10);
      user.password = hashedPassword;
      await user.save();
      console.log(`✅ Password hashé pour : ${user.email}`);
    }

    console.log('🎉 Tous les mots de passe sont sécurisés.');
    mongoose.disconnect();
  } catch (err) {
    console.error('❌ Erreur :', err);
  }
};

hashUnhashedPasswords();
