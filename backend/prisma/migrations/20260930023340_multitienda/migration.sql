/*
  Warnings:

  - A unique constraint covering the columns `[sucursal_id,sku]` on the table `productos` will be added. If there are existing duplicate values, this will fail.
  - Added the required column `sucursal_id` to the `detalle_ventas` table without a default value. This is not possible if the table is not empty.
  - Added the required column `sucursal_id` to the `productos` table without a default value. This is not possible if the table is not empty.
  - Added the required column `sucursal_id` to the `venta_tickets` table without a default value. This is not possible if the table is not empty.

*/
-- DropIndex
DROP INDEX "productos_sku_key";

-- AlterTable
ALTER TABLE "detalle_ventas" ADD COLUMN     "sucursal_id" TEXT NOT NULL;

-- AlterTable
ALTER TABLE "productos" ADD COLUMN     "sucursal_id" TEXT NOT NULL;

-- AlterTable
ALTER TABLE "usuarios" ADD COLUMN     "sucursal_id" TEXT;

-- AlterTable
ALTER TABLE "venta_tickets" ADD COLUMN     "sucursal_id" TEXT NOT NULL;

-- CreateTable
CREATE TABLE "sucursales" (
    "id" TEXT NOT NULL,
    "nombre" TEXT NOT NULL,
    "direccion" TEXT,
    "updated_at" TIMESTAMP(3) NOT NULL,
    "device_id" TEXT NOT NULL,

    CONSTRAINT "sucursales_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "productos_sucursal_id_sku_key" ON "productos"("sucursal_id", "sku");
