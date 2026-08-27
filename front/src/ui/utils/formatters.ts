/**
 * Utilidades de formateo para la interfaz de usuario.
 */

/**
 * Formatea un identificador para visualización limpia en tablas y listas.
 * Muestra una secuencia numerada amigable (ej. #USR-001, #PRD-002) manteniendo
 * el UUID v7 real en el atributo `title` para debugging e inspección.
 * 
 * @param prefix Prefijo del módulo ('USR', 'PRD', 'LOT', 'EMP', etc.)
 * @param index Índice de la fila (0-indexed, opcional)
 * @param uuid UUID completo para fallback (opcional)
 */
export function formatDisplayId(prefix: string, index?: number, uuid?: string): string {
  if (index !== undefined && index !== null && index >= 0) {
    const seq = String(index + 1).padStart(3, '0');
    return `#${prefix}-${seq}`;
  }
  if (uuid) {
    // Si no hay índice, tomamos los primeros 8 caracteres del UUID v7
    const shortHex = uuid.split('-')[0] || uuid.substring(0, 8);
    return `#${prefix}-${shortHex}`;
  }
  return `#${prefix}-001`;
}
