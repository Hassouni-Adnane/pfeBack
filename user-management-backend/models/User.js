const mongoose = require('mongoose');
const bcrypt = require('bcrypt');

const userSchema = new mongoose.Schema({
  id: { type: Number, required: true, unique: true },
  name: { type: String, required: true },
  email: { type: String, required: true, unique: true },
  jobTitle: { type: String },
  password: { type: String, required: true },
  status: { type: String, enum: ['Approved', 'Pending', 'Archived'], default: 'Pending' },
  role: { type: String, enum: ['admin', 'user'], default: 'user' }
});

// 🔐 Hash le mot de passe avant d'enregistrer
userSchema.pre('save', async function (next) {
  const user = this;
  if (!user.isModified('password')) return next(); // Ne re-hash pas si déjà fait

  try {
    const salt = await bcrypt.genSalt(10);
    user.password = await bcrypt.hash(user.password, salt);
    next();
  } catch (err) {
    next(err);
  }
});

module.exports = mongoose.model('User', userSchema);
