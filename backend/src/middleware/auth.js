// Autenticación simple para el alcance del reto: un token fijo compartido
// entre el cliente MAUI y el API, vía variable de entorno.
// No es OAuth ni JWT con expiración — es suficiente para demostrar el
// flujo de sincronización sin meter complejidad extra fuera del alcance.

function requireAuth(req, res, next) {
  const header = req.headers.authorization || '';
  const token = header.startsWith('Bearer ') ? header.slice(7) : null;

  if (!token || token !== process.env.API_TOKEN) {
    return res.status(401).json({ error: 'No autorizado' });
  }

  next();
}

module.exports = requireAuth;
