import { JSX, For, Show } from 'solid-js';

export interface Column<T> {
  header: string;
  accessor?: keyof T;
  cell?: (row: T) => JSX.Element;
  className?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  loading?: boolean;
  emptyMessage?: string;
}

export function DataTable<T extends { id: string | number }>(props: DataTableProps<T>) {
  return (
    <div class="w-full overflow-x-auto rounded-xl border border-slate-700/60 bg-slate-900/40 shadow-xl backdrop-blur-md">
      <table class="w-full min-w-[700px] text-left border-collapse text-sm text-slate-200">
        <thead class="bg-slate-800/80 text-xs uppercase tracking-wider text-slate-400 font-semibold border-b border-slate-700/60">
          <tr>
            <For each={props.columns}>
              {(col) => (
                <th class={`py-3.5 px-4 ${col.className || ''}`}>
                  {col.header}
                </th>
              )}
            </For>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-800/60">
          <Show
            when={!props.loading}
            fallback={
              <tr>
                <td colSpan={props.columns.length} class="py-8 text-center text-slate-400">
                  <div class="inline-flex items-center space-x-2">
                    <svg class="animate-spin h-5 w-5 text-emerald-400" viewBox="0 0 24 24" fill="none">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
                    </svg>
                    <span>Cargando datos...</span>
                  </div>
                </td>
              </tr>
            }
          >
            <Show
              when={props.data.length > 0}
              fallback={
                <tr>
                  <td colSpan={props.columns.length} class="py-10 text-center text-slate-400 font-medium">
                    {props.emptyMessage || 'No se encontraron registros'}
                  </td>
                </tr>
              }
            >
              <For each={props.data}>
                {(row) => (
                  <tr class="hover:bg-slate-800/50 transition-colors duration-150 group">
                    <For each={props.columns}>
                      {(col) => (
                        <td class={`py-3.5 px-4 align-middle ${col.className || ''}`}>
                          {col.cell ? col.cell(row) : (col.accessor ? String(row[col.accessor] ?? '') : '')}
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
  );
}
