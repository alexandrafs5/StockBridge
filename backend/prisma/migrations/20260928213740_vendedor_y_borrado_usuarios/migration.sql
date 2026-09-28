-- AlterTable
ALTER TABLE "usuarios" ADD COLUMN     "deleted" BOOLEAN NOT NULL DEFAULT false;

-- AlterTable
ALTER TABLE "venta_tickets" ADD COLUMN     "vendedor_id" TEXT,
ADD COLUMN     "vendedor_nombre" TEXT;
