const express = require('express');
const prisma = require('../db');

const router = express.Router();

/**
 * POST /sync/push
 * Recibe un batch de operaciones generadas offline por el cliente.
 *
 * Body esperado:
 * {
 *   "operaciones": [
 *     {
 *       "id": "uuid-de-la-operacion",   // generado en el cliente, usado para idempotencia
 *       "tabla": "productos" | "venta_tickets" | "detalle_ventas",
 *       "operacion": "create" | "update" | "delete",
 *       "payload": { ... },              // datos del registro
 *       "updatedAt": "2026-09-26T10:05:00Z",
 *       "deviceId": "device-abc"
 *     }
 *   ]
 * }
 */
router.post('/push', async (req, res) => {
  const { operaciones } = req.body;

  if (!Array.isArray(operaciones)) {
    return res.status(400).json({ error: 'Se esperaba un arreglo "operaciones"' });
  }

  const resultados = [];

  for (const op of operaciones) {
    try {
      const resultado = await procesarOperacion(op);
      resultados.push({ id: op.id, status: resultado });
    } catch (err) {
      console.error(`Error procesando operación ${op.id}:`, err);
      resultados.push({ id: op.id, status: 'error', mensaje: err.message });
    }
  }

  res.json({ resultados });
});

/**
 * GET /sync/pull?since=<ISO timestamp>&deviceId=<id>
 * Devuelve los cambios ocurridos después de "since", excluyendo los que
 * originó el mismo dispositivo que pregunta (para no hacerle echo de sus
 * propios cambios).
 */
router.get('/pull', async (req, res) => {
  const { since, deviceId } = req.query;
  const sinceDate = since ? new Date(since) : new Date(0);

  const whereBase = {
    updatedAt: { gt: sinceDate },
    ...(deviceId ? { deviceId: { not: deviceId } } : {}),
  };

  const [productos, ventaTickets, detalleVentas, usuarios] = await Promise.all([
    prisma.producto.findMany({ where: whereBase }),
    prisma.ventaTicket.findMany({ where: whereBase }),
    prisma.detalleVenta.findMany({ where: whereBase }),
    prisma.usuario.findMany({ where: whereBase }),
  ]);

  res.json({
    servidorTimestamp: new Date().toISOString(),
    productos,
    ventaTickets,
    detalleVentas,
    usuarios,
  });
});

/**
 * Aplica una operación individual con:
 * - Idempotencia: si el id de la operación ya fue procesado, se ignora.
 * - Last-write-wins: solo se aplica si updatedAt es más reciente que lo almacenado.
 */
async function procesarOperacion(op) {
  const yaProcesada = await prisma.syncLog.findUnique({ where: { id: op.id } });
  if (yaProcesada) {
    return 'duplicado-ignorado';
  }

  const modelo = obtenerModelo(op.tabla);
  const existente = await modelo.findUnique({ where: { id: op.payload.id } });

  if (existente && new Date(existente.updatedAt) >= new Date(op.updatedAt)) {
    // El servidor ya tiene una versión igual o más reciente: se descarta esta operación.
    await registrarComoProcesada(op);
    return 'conflicto-descartado';
  }

  if (op.operacion === 'delete') {
    await modelo.update({
      where: { id: op.payload.id },
      data: { deleted: true, updatedAt: op.updatedAt, deviceId: op.deviceId },
    });
  } else {
    await modelo.upsert({
      where: { id: op.payload.id },
      create: { ...op.payload, updatedAt: op.updatedAt, deviceId: op.deviceId },
      update: { ...op.payload, updatedAt: op.updatedAt, deviceId: op.deviceId },
    });
  }

  await registrarComoProcesada(op);
  return 'aplicada';
}

async function registrarComoProcesada(op) {
  await prisma.syncLog.create({
    data: { id: op.id, tabla: op.tabla, operacion: op.operacion },
  });
}

function obtenerModelo(tabla) {
  switch (tabla) {
    case 'productos':
      return prisma.producto;
    case 'venta_tickets':
      return prisma.ventaTicket;
    case 'detalle_ventas':
      return prisma.detalleVenta;
    case 'usuarios':
      return prisma.usuario;
    default:
      throw new Error(`Tabla desconocida: ${tabla}`);
  }
}

module.exports = router;
