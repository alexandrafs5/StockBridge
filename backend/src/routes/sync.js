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
 *       "tabla": "productos" | "venta_tickets" | "detalle_ventas" | "usuarios" | "sucursales",
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
 * GET /sync/pull?since=<ISO timestamp>&deviceId=<id>&sucursalId=<id opcional>
 *
 * Devuelve los cambios ocurridos después de "since", excluyendo los que
 * originó el mismo dispositivo que pregunta.
 *
 * Si el dispositivo TODAVÍA no tiene una sucursal asignada (no manda
 * sucursalId), es un equipo nuevo en configuración: solo recibe la lista
 * de sucursales y los usuarios con rol "dueno" — lo mínimo para decidir
 * a qué sucursal pertenece o crear una nueva. No recibe inventario ni
 * ventas de sucursales que todavía no le corresponden.
 *
 * Una vez que el dispositivo ya tiene sucursalId, el pull queda filtrado
 * a esa sucursal para productos/ventas, y a "empleados de esa sucursal +
 * cualquier dueño" para usuarios.
 */
router.get('/pull', async (req, res) => {
  const { since, deviceId, sucursalId } = req.query;
  const sinceDate = since ? new Date(since) : new Date(0);

  const whereBase = {
    updatedAt: { gt: sinceDate },
    ...(deviceId ? { deviceId: { not: deviceId } } : {}),
  };

  // Todos los dispositivos necesitan conocer la lista de sucursales
  // (para el selector de "en qué sucursal está este equipo" y para
  // poder agregar sucursales nuevas).
  const sucursales = await prisma.sucursal.findMany({ where: whereBase });

  if (!sucursalId) {
    const duenos = await prisma.usuario.findMany({
      where: { ...whereBase, rol: 'dueno' },
    });

    return res.json({
      servidorTimestamp: new Date().toISOString(),
      sucursales,
      usuarios: duenos,
      productos: [],
      ventaTickets: [],
      detalleVentas: [],
    });
  }

  const whereSucursal = { ...whereBase, sucursalId };
  const whereUsuarios = { ...whereBase, OR: [{ sucursalId }, { rol: 'dueno' }] };

  const [productos, ventaTickets, detalleVentas, usuarios] = await Promise.all([
    prisma.producto.findMany({ where: whereSucursal }),
    prisma.ventaTicket.findMany({ where: whereSucursal }),
    prisma.detalleVenta.findMany({ where: whereSucursal }),
    prisma.usuario.findMany({ where: whereUsuarios }),
  ]);

  res.json({
    servidorTimestamp: new Date().toISOString(),
    productos,
    ventaTickets,
    detalleVentas,
    usuarios,
    sucursales,
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
    case 'sucursales':
      return prisma.sucursal;
    default:
      throw new Error(`Tabla desconocida: ${tabla}`);
  }
}

module.exports = router;
