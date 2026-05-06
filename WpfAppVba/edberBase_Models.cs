// ============================================================
//  edberBase — Modelos C# para Dapper
//  Generado desde: 20260422_edberBase.bacpac
//  Uso: Install-Package Dapper en tu proyecto WPF
// ============================================================

using System;

namespace EdberBase.Models
{
    // ─────────────────────────────────────────────
    //  CATÁLOGOS SIMPLES
    // ─────────────────────────────────────────────

    public class Categoria
    {
        public double  Id          { get; set; }
        public string  Descripcion { get; set; }
        public string  Estadof     { get; set; }
    }

    public class Producto
    {
        public double  Id          { get; set; }
        public string  Descripcion { get; set; }
        public string  Estadof     { get; set; }
    }

    public class Familia
    {
        public double   Id          { get; set; }
        public double   Producto    { get; set; }
        public string   Descripcion { get; set; }
        public string   Estadof     { get; set; }
        public string   Observacion { get; set; }
        public DateTime Emision     { get; set; }
        public DateTime Edicion     { get; set; }
    }

    public class Industria
    {
        public double  Id          { get; set; }
        public string  Descripcion { get; set; }
        public string  Estadof     { get; set; }
    }

    public class Region
    {
        public double  Id          { get; set; }
        public string  Descripcion { get; set; }
        public string  Estadof     { get; set; }
    }

    // ─────────────────────────────────────────────
    //  ENTIDADES PRINCIPALES
    // ─────────────────────────────────────────────

    public class Articulo
    {
        public double   Id          { get; set; }
        public string   Descripcion { get; set; }
        public double   Indice      { get; set; }
        public double   Categoria   { get; set; }
        public double   Familia     { get; set; }
        public double   Industria   { get; set; }
        public string   Modelo      { get; set; }
        public string   Observacion { get; set; }
        public string   Estado      { get; set; }
        public string   Estadof     { get; set; }
        public DateTime Emision     { get; set; }
        public DateTime Edicion     { get; set; }
        public double   Usuario     { get; set; }
        public string   Codigo      { get; set; }
    }

    public class Sucursal
    {
        public double   Id          { get; set; }
        public string   Nit         { get; set; }
        public string   Descripcion { get; set; }
        public string   Direccion   { get; set; }
        public double   Region      { get; set; }
        public string   Telefono    { get; set; }
        public string   Observacion { get; set; }
        public string   Estadof     { get; set; }
        public DateTime Emision     { get; set; }
        public DateTime Edicion     { get; set; }
        public double   Usuario     { get; set; }
        public DateTime Fecha       { get; set; }
    }

    public class Tercero
    {
        public double   Id          { get; set; }
        public double   Nit         { get; set; }
        public string   Descripcion { get; set; }
        public string   Telefono    { get; set; }
        public string   Contacto    { get; set; }
        public string   Direccion   { get; set; }
        public string   Contacto2   { get; set; }
        public string   Telefono2   { get; set; }
        public string   Observacion { get; set; }
        public string   Estadof     { get; set; }
        public DateTime Emision     { get; set; }
        public DateTime Edicion     { get; set; }
        public double   Usuario     { get; set; }
    }

    public class Usuario
    {
        public double  Id         { get; set; }
        public string  Cuenta     { get; set; }
        public string  Llave      { get; set; }
        public string  Nombres    { get; set; }
        public string  Apellidos  { get; set; }
        public double  Sucursal   { get; set; }
        public string  Estadof    { get; set; }
        public string  Tipo       { get; set; }
    }

    // ─────────────────────────────────────────────
    //  STOCK Y PRECIOS
    // ─────────────────────────────────────────────

    public class Stock
    {
        public double  Id        { get; set; }
        public double  Sucursal  { get; set; }
        public double  Indice    { get; set; }
        public double  Articulo  { get; set; }
        public string  Estadof   { get; set; }
    }

    public class Precio
    {
        public double   Id       { get; set; }
        public DateTime Fecha    { get; set; }
        public double   Region   { get; set; }
        public double   Articulo { get; set; }
        public double   Precio_  { get; set; }  // "precio" reservado, se mapea con alias
        public string   Estadof  { get; set; }
    }

    // ─────────────────────────────────────────────
    //  DOCUMENTOS
    // ─────────────────────────────────────────────

    public class DocumentoP   // Compras/Ventas
    {
        public double   Id          { get; set; }
        public DateTime Fecha       { get; set; }
        public double   Sucursal    { get; set; }
        public string   Estado      { get; set; }
        public string   Tipo        { get; set; }
        public double   Emitido     { get; set; }
        public DateTime Emision     { get; set; }
        public DateTime Edicion     { get; set; }
        public double   Usuario     { get; set; }
        public string   Referencia  { get; set; }
        public string   Estadof     { get; set; }
        public string   Movimiento  { get; set; }
        public string   Observacion { get; set; }
        public string   Tercero     { get; set; }
        public string   EstadoC     { get; set; }
    }

    public class DocumentoT   // Traspasos entre sucursales
    {
        public double   Id          { get; set; }
        public double   Origen      { get; set; }
        public double   Destino     { get; set; }
        public DateTime Fecha       { get; set; }
        public string   Estado      { get; set; }
        public double   Emitido     { get; set; }
        public DateTime Emision     { get; set; }
        public DateTime Edicion     { get; set; }
        public double   Usuario     { get; set; }
        public string   Referencia  { get; set; }
        public string   Estadof     { get; set; }
        public string   Observacion { get; set; }
    }

    public class DocumentoI   // Inventarios
    {
        public double   Id          { get; set; }
        public double   Sucursal    { get; set; }
        public string   Observacion { get; set; }
        public DateTime Fecha       { get; set; }
        public DateTime Emision     { get; set; }
        public DateTime Edicion     { get; set; }
        public double   Usuario     { get; set; }
        public string   Estadof     { get; set; }
    }

    // ─────────────────────────────────────────────
    //  DETALLES / LÍNEAS
    // ─────────────────────────────────────────────

    public class Pedido        // Líneas de DocumentoP
    {
        public double  Id         { get; set; }
        public double  DocumentoP { get; set; }
        public double  Indice     { get; set; }
        public double  Articulo   { get; set; }
        public double  Cantidad   { get; set; }
        public double  Importe    { get; set; }
        public string  Tipo       { get; set; }
        public string  Estadof    { get; set; }
        public string  Forma      { get; set; }
        public double  Contable   { get; set; }
    }

    public class Traspaso      // Líneas de DocumentoT
    {
        public double  Id         { get; set; }
        public double  DocumentoT { get; set; }
        public double  Indice     { get; set; }
        public double  Articulo   { get; set; }
        public double  Cantidad   { get; set; }
        public string  Estadof    { get; set; }
    }

    public class Inventario    // Líneas de DocumentoI
    {
        public double  Id         { get; set; }
        public double  DocumentoI { get; set; }
        public double  Indice     { get; set; }
        public double  Articulo   { get; set; }
        public double  Cantidad   { get; set; }
        public string  Estadof    { get; set; }
    }

    public class Entrega       // Entregas de DocumentoP
    {
        public double   Id         { get; set; }
        public double   DocumentoP { get; set; }
        public double   Indice     { get; set; }
        public double   Articulo   { get; set; }
        public double   Cantidad   { get; set; }
        public DateTime Fecha      { get; set; }
        public string   Estadof    { get; set; }
    }

    public class Transaccion   // Pagos/cobros
    {
        public double   Id         { get; set; }
        public DateTime Fecha      { get; set; }
        public string   Descripcion{ get; set; }
        public double   DocumentoP { get; set; }
        public double   Indice     { get; set; }
        public double   Importe    { get; set; }
        public string   Forma      { get; set; }
        public string   Estadof    { get; set; }
    }

    public class Gasto
    {
        public double   Id          { get; set; }
        public DateTime Fecha       { get; set; }
        public double   Origen      { get; set; }
        public string   Descripcion { get; set; }
        public double   Importe     { get; set; }
        public string   Estadof     { get; set; }
    }
}


// ============================================================
//  REPOSITORIO GENÉRICO CON DAPPER
//  Archivo sugerido: Data/EdberRepository.cs
// ============================================================
/*
using Dapper;
using System.Data.SqlClient;
using EdberBase.Models;

public class EdberRepository
{
    private readonly string _cs;
    public EdberRepository(string connectionString) => _cs = connectionString;

    // --- ARTÍCULOS ---
    public IEnumerable<Articulo> GetArticulos(string soloActivos = "A") =>
        Query<Articulo>("SELECT * FROM articulos WHERE estadof = @e", new { e = soloActivos });

    public Articulo GetArticulo(double id) =>
        QueryFirst<Articulo>("SELECT * FROM articulos WHERE id = @id", new { id });

    // --- PEDIDOS (detalle de documento) ---
    public IEnumerable<Pedido> GetPedidosPorDocumento(double docId) =>
        Query<Pedido>("SELECT * FROM pedidos WHERE documentoP = @docId AND estadof='A'", new { docId });

    // --- STOCKS ---
    public IEnumerable<Stock> GetStockPorSucursal(double sucursalId) =>
        Query<Stock>("SELECT * FROM stocks WHERE sucursal = @s AND estadof='A'", new { s = sucursalId });

    // --- PRECIOS (último precio por artículo y región) ---
    public IEnumerable<Precio> GetUltimosPrecios(double regionId) =>
        Query<Precio>(@"
            SELECT p.*
            FROM precios p
            INNER JOIN (
                SELECT articulo, MAX(fecha) AS uf
                FROM precios WHERE region = @r AND estadof='A'
                GROUP BY articulo
            ) lp ON p.articulo = lp.articulo AND p.fecha = lp.uf
            WHERE p.region = @r", new { r = regionId });

    // --- HELPER ---
    private IEnumerable<T> Query<T>(string sql, object param = null)
    {
        using var conn = new SqlConnection(_cs);
        return conn.Query<T>(sql, param);
    }

    private T QueryFirst<T>(string sql, object param = null)
    {
        using var conn = new SqlConnection(_cs);
        return conn.QueryFirstOrDefault<T>(sql, param);
    }
}
*/
