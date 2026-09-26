require('dotenv').config();
const express = require('express');
const cors = require('cors');

const requireAuth = require('./middleware/auth');
const syncRoutes = require('./routes/sync');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json({ limit: '5mb' })); // batches de sync pueden traer varios registros

// Health check sin auth — útil para que Railway/Docker verifiquen que el contenedor está vivo
app.get('/health', (req, res) => {
  res.json({ status: 'ok' });
});

app.use('/sync', requireAuth, syncRoutes);

app.listen(PORT, () => {
  console.log(`StockBridge API corriendo en el puerto ${PORT}`);
});
