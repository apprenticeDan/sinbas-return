import { JSX, For, Show } from 'solid-js';

export interface Column<T> {
  header: string;
  accessor?: keyof T;
  cell?: (row: T, index: number) => JSX.Element;
  className?: string;
  align?: 'left' | 'center' | 'right';
  width?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  loading?: boolean;
  emptyMessage?: string;
}

export function DataTable<T extends { id: string | number }>(props: DataTableProps<T>) {
  return (
    <div class="card" style={{ overflow: 'hidden' }}>
      <div style={{ 'overflow-x': 'auto', width: '100%' }}>
        <table style={{ width: '100%', 'border-collapse': 'collapse', 'min-width': '650px' }}>
          <thead>
            <tr>
              <For each={props.columns}>
                {(col) => (
                  <th
                    class={col.className || ''}
                    style={{ 'text-align': col.align || 'left', width: col.width || 'auto' }}
                  >
                    {col.header}
                  </th>
                )}
              </For>
            </tr>
          </thead>
          <tbody>
            <Show
              when={!props.loading}
              fallback={
                <tr>
                  <td colSpan={props.columns.length} style={{ padding: '36px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
                    <div style={{ display: 'inline-flex', 'align-items': 'center', gap: '8px' }}>
                      <span class="animate-spin">↻</span>
                      <span>Cargando información...</span>
                    </div>
                  </td>
                </tr>
              }
            >
              <Show
                when={props.data.length > 0}
                fallback={
                  <tr>
                    <td colSpan={props.columns.length} style={{ padding: '36px', 'text-align': 'center', color: 'var(--ink-soft)', 'font-weight': '500' }}>
                      {props.emptyMessage || 'No se encontraron registros'}
                    </td>
                  </tr>
                }
              >
                <For each={props.data}>
                  {(row, idx) => (
                    <tr>
                      <For each={props.columns}>
                        {(col) => (
                          <td
                            class={col.className || ''}
                            style={{ 'text-align': col.align || 'left', width: col.width || 'auto' }}
                          >
                            {col.cell ? col.cell(row, idx()) : (col.accessor ? String(row[col.accessor] ?? '') : '')}
                          </td>
                        )}
                      </For>
                    </tr>
                  )}
                </For>
              </Show>
            </Show>
          </tbody>
        </table>
      </div>
    </div>
  );
}
