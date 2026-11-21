import { For, Show, type JSX } from 'solid-js';

export interface FilterSelectOption {
  value: string;
  label: string;
}

export interface FilterSelectConfig {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: FilterSelectOption[];
}

export interface SortSelectConfig {
  value: string;
  onChange: (value: string) => void;
  options: FilterSelectOption[];
}

export interface FiltersBarProps {
  search?: {
    value: string;
    onChange: (v: string) => void;
    placeholder?: string;
  };
  filters?: FilterSelectConfig[];
  sort?: SortSelectConfig;
  onClear?: () => void;
  onRefresh?: () => void;
  autoRefresh?: {
    enabled: boolean;
    onChange: (enabled: boolean) => void;
  };
}

export function FiltersBar(props: FiltersBarProps): JSX.Element {
  return (
    <div class="filters-bar">
      <Show when={props.search}>
        {(search) => (
          <div class="filters-bar__search">
            <input
              type="text"
              placeholder={search().placeholder ?? 'Buscar...'}
              value={search().value}
              onInput={(e) => search().onChange(e.currentTarget.value)}
            />
          </div>
        )}
      </Show>

      <Show when={props.filters && props.filters.length > 0}>
        <For each={props.filters}>
          {(filter) => (
            <div class="filters-bar__filter">
              <label class="filters-bar__filter-label">{filter.label}</label>
              <select
                value={filter.value}
                onChange={(e) => filter.onChange(e.currentTarget.value)}
              >
                <For each={filter.options}>
                  {(option) => (
                    <option value={option.value}>{option.label}</option>
                  )}
                </For>
              </select>
            </div>
          )}
        </For>
      </Show>

      <Show when={props.sort}>
        {(sort) => (
          <div class="filters-bar__sort">
            <label class="filters-bar__sort-label">Ordenar por</label>
            <select
              value={sort().value}
              onChange={(e) => sort().onChange(e.currentTarget.value)}
            >
              <For each={sort().options}>
                {(option) => (
                  <option value={option.value}>{option.label}</option>
                )}
              </For>
            </select>
          </div>
        )}
      </Show>

      <Show when={props.onRefresh}>
        {(onRefresh) => (
          <button
            type="button"
            class="filters-bar__refresh"
            onClick={() => onRefresh()()}
          >
            Actualizar
          </button>
        )}
      </Show>

      <Show when={props.autoRefresh}>
        {(auto) => (
          <label class="filters-bar__auto">
            <input
              type="checkbox"
              checked={auto().enabled}
              onChange={(e) => auto().onChange(e.currentTarget.checked)}
            />
            <span>Auto-refrescar</span>
          </label>
        )}
      </Show>

      <Show when={props.onClear}>
        {(onClear) => (
          <button
            type="button"
            class="filters-bar__clear"
            onClick={() => onClear()()}
          >
            Limpiar
          </button>
        )}
      </Show>
    </div>
  );
}
