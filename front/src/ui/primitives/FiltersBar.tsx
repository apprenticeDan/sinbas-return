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
  id?: string;
  name?: string;
}

export interface SortSelectConfig {
  value: string;
  onChange: (value: string) => void;
  options: FilterSelectOption[];
  id?: string;
  name?: string;
}

export interface FiltersBarProps {
  idPrefix?: string;
  search?: {
    value: string;
    onChange: (v: string) => void;
    placeholder?: string;
    id?: string;
    name?: string;
  };
  filters?: FilterSelectConfig[];
  sort?: SortSelectConfig;
  onClear?: () => void;
  onRefresh?: () => void;
  autoRefresh?: {
    enabled: boolean;
    onChange: (enabled: boolean) => void;
    id?: string;
    name?: string;
  };
}

export function FiltersBar(props: FiltersBarProps): JSX.Element {
  const prefix = () => props.idPrefix ?? 'filters-bar';

  return (
    <div class="filters-bar">
      <Show when={props.search}>
        {(search) => {
          const searchId = search().id ?? `${prefix()}-search`;
          const searchName = search().name ?? 'search';
          return (
            <div class="filters-bar__search">
              <label
                for={searchId}
                style={{
                  position: 'absolute',
                  width: '1px',
                  height: '1px',
                  padding: '0',
                  margin: '-1px',
                  overflow: 'hidden',
                  clip: 'rect(0, 0, 0, 0)',
                  'white-space': 'nowrap',
                  'border-width': '0',
                }}
              >
                {search().placeholder ?? 'Buscar'}
              </label>
              <input
                id={searchId}
                name={searchName}
                type="text"
                aria-label={search().placeholder ?? 'Buscar'}
                placeholder={search().placeholder ?? 'Buscar...'}
                value={search().value}
                onInput={(e) => search().onChange(e.currentTarget.value)}
              />
            </div>
          );
        }}
      </Show>

      <Show when={props.filters && props.filters.length > 0}>
        <For each={props.filters}>
          {(filter) => {
            const filterId =
              filter.id ??
              `${prefix()}-filter-${filter.label.toLowerCase().replace(/[^a-z0-9]/g, '-')}`;
            const filterName =
              filter.name ?? filter.label.toLowerCase().replace(/[^a-z0-9]/g, '-');
            return (
              <div class="filters-bar__filter">
                <label for={filterId} class="filters-bar__filter-label">
                  {filter.label}
                </label>
                <select
                  id={filterId}
                  name={filterName}
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
            );
          }}
        </For>
      </Show>

      <Show when={props.sort}>
        {(sort) => {
          const sortId = sort().id ?? `${prefix()}-sort`;
          const sortName = sort().name ?? 'sort';
          return (
            <div class="filters-bar__sort">
              <label for={sortId} class="filters-bar__sort-label">
                Ordenar por
              </label>
              <select
                id={sortId}
                name={sortName}
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
          );
        }}
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
        {(auto) => {
          const autoId = auto().id ?? `${prefix()}-auto-refresh`;
          const autoName = auto().name ?? 'autoRefresh';
          return (
            <label for={autoId} class="filters-bar__auto">
              <input
                id={autoId}
                name={autoName}
                type="checkbox"
                checked={auto().enabled}
                onChange={(e) => auto().onChange(e.currentTarget.checked)}
              />
              <span>Auto-refrescar</span>
            </label>
          );
        }}
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
