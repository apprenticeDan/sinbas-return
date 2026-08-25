import { Component, createSignal, For, Show, onCleanup, onMount } from 'solid-js';
import { SystemRole, AVAILABLE_ROLES } from '../../domain/models/Role';

interface RoleSelectProps {
  selected: SystemRole[];
  onChange: (roles: SystemRole[]) => void;
}

export const RoleSelect: Component<RoleSelectProps> = (props) => {
  const [isOpen, setIsOpen] = createSignal(false);
  const [filterText, setFilterText] = createSignal('');
  let containerRef: HTMLDivElement | undefined;

  const filteredRoles = () => {
    const query = filterText().toLowerCase().trim();
    if (!query) return AVAILABLE_ROLES;
    return AVAILABLE_ROLES.filter((role) => role.toLowerCase().includes(query));
  };

  const toggleRole = (role: SystemRole) => {
    if (props.selected.includes(role)) {
      props.onChange(props.selected.filter((r) => r !== role));
    } else {
      props.onChange([...props.selected, role]);
    }
  };

  const removeRole = (e: MouseEvent, role: SystemRole) => {
    e.stopPropagation();
    props.onChange(props.selected.filter((r) => r !== role));
  };

  const handleClickOutside = (e: MouseEvent) => {
    if (containerRef && !containerRef.contains(e.target as Node)) {
      setIsOpen(false);
    }
  };

  onMount(() => {
    document.addEventListener('click', handleClickOutside);
  });

  onCleanup(() => {
    document.removeEventListener('click', handleClickOutside);
  });

  return (
    <div ref={containerRef} style={{ position: 'relative', width: '100%' }}>
      {/* Selected Tags Display */}
      <div
        class="role-select-box"
        onClick={() => setIsOpen(!isOpen())}
        style={{
          background: 'var(--surface-alt)',
          border: '1px solid var(--border)',
          'border-radius': 'var(--radius-s)',
          padding: '6px 10px',
          'min-height': '40px',
          display: 'flex',
          'flex-wrap': 'wrap',
          'align-items': 'center',
          gap: '6px',
          cursor: 'pointer',
        }}
      >
        <Show
          when={props.selected.length > 0}
          fallback={<span style={{ color: 'var(--ink-faint)', 'font-size': '13px' }}>Buscar o seleccionar roles...</span>}
        >
          <For each={props.selected}>
            {(role) => (
              <span
                class="pill pill-green"
                style={{ 'font-size': '11px', padding: '3px 8px', display: 'inline-flex', 'align-items': 'center', gap: '4px' }}
              >
                {role}
                <button
                  type="button"
                  onClick={(e) => removeRole(e, role)}
                  style={{
                    border: 'none',
                    background: 'transparent',
                    color: 'currentColor',
                    cursor: 'pointer',
                    padding: '0 2px',
                    'font-weight': 'bold',
                    'font-size': '11px',
                  }}
                >
                  ✕
                </button>
              </span>
            )}
          </For>
        </Show>
        <span style={{ 'margin-left': 'auto', opacity: 0.5, 'font-size': '11px' }}>▼</span>
      </div>

      {/* Dropdown Menu */}
      <Show when={isOpen()}>
        <div
          class="role-select-dropdown"
          style={{
            position: 'absolute',
            top: '100%',
            left: 0,
            right: 0,
            'margin-top': '4px',
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            'border-radius': 'var(--radius-s)',
            'box-shadow': '0 8px 20px rgba(0,0,0,0.15)',
            'z-index': 1100,
            padding: '8px',
          }}
        >
          {/* Search Filter Textbox */}
          <input
            type="text"
            placeholder="Filtrar rol por texto..."
            value={filterText()}
            onInput={(e) => setFilterText(e.currentTarget.value)}
            style={{
              width: '100%',
              padding: '6px 10px',
              'font-size': '12.5px',
              border: '1px solid var(--border)',
              'border-radius': 'var(--radius-s)',
              background: 'var(--surface-alt)',
              'margin-bottom': '6px',
              'box-sizing': 'border-box',
            }}
            onClick={(e) => e.stopPropagation()}
          />

          <div style={{ 'max-height': '160px', 'overflow-y': 'auto', display: 'flex', 'flex-direction': 'column', gap: '2px' }}>
            <For each={filteredRoles()}>
              {(role) => {
                const isSelected = () => props.selected.includes(role);
                return (
                  <div
                    onClick={() => toggleRole(role)}
                    style={{
                      padding: '7px 10px',
                      'border-radius': 'var(--radius-s)',
                      cursor: 'pointer',
                      display: 'flex',
                      'align-items': 'center',
                      'justify-content': 'space-between',
                      'font-size': '13px',
                      background: isSelected() ? 'var(--green-tint)' : 'transparent',
                      color: isSelected() ? 'var(--green-deep)' : 'var(--ink)',
                      'font-weight': isSelected() ? '600' : '400',
                    }}
                  >
                    <span>{role}</span>
                    <input
                      type="checkbox"
                      checked={isSelected()}
                      onChange={() => {}}
                      style={{ cursor: 'pointer' }}
                    />
                  </div>
                );
              }}
            </For>
            <Show when={filteredRoles().length === 0}>
              <div style={{ padding: '8px', 'font-size': '12px', color: 'var(--ink-soft)', 'text-align': 'center' }}>
                No se encontraron roles
              </div>
            </Show>
          </div>
        </div>
      </Show>
    </div>
  );
};
