import { createSignal, Show } from 'solid-js';
import { ApiClientGateway, CrearClienteNaturalPayload, CrearClienteJuridicaPayload } from '../../infrastructure/api/ApiClientGateway';

interface ClienteFormModalProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function ClienteFormModal(props: ClienteFormModalProps) {
  const [tipo, setTipo] = createSignal<'Natural' | 'Juridica'>('Natural');

  // Campos Natural
  const [nombres, setNombres] = createSignal('');
  const [apellidoPaterno, setApellidoPaterno] = createSignal('');
  const [apellidoMaterno, setApellidoMaterno] = createSignal('');
  const [ciNumero, setCiNumero] = createSignal('');
  const [ciComplemento, setCiComplemento] = createSignal('');
  const [ciExtension, setCiExtension] = createSignal('CB');

  // Campos Jurídica
  const [razonSocial, setRazonSocial] = createSignal('');
  const [nit, setNit] = createSignal('');
  const [hasRep, setHasRep] = createSignal(false);
  const [repNombres, setRepNombres] = createSignal('');
  const [repPaterno, setRepPaterno] = createSignal('');
  const [repMaterno, setRepMaterno] = createSignal('');
  const [repCi, setRepCi] = createSignal('');
  const [repExt, setRepExt] = createSignal('CB');

  // Campos Comunes
  const [telefono, setTelefono] = createSignal('');
  const [email, setEmail] = createSignal('');
  const [direccion, setDireccion] = createSignal('');

  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  const resetForm = () => {
    setNombres('');
    setApellidoPaterno('');
    setApellidoMaterno('');
    setCiNumero('');
    setCiComplemento('');
    setCiExtension('CB');
    setRazonSocial('');
    setNit('');
    setHasRep(false);
    setRepNombres('');
    setRepPaterno('');
    setRepMaterno('');
    setRepCi('');
    setRepExt('CB');
    setTelefono('');
    setEmail('');
    setDireccion('');
    setError(null);
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      if (tipo() === 'Natural') {
        if (!apellidoPaterno().trim() && !apellidoMaterno().trim()) {
          setError('Debe ingresar al menos un apellido (paterno o materno)');
          setSubmitting(false);
          return;
        }

        const payload: CrearClienteNaturalPayload = {
          nombres: nombres().trim(),
          apellidoPaterno: apellidoPaterno().trim() || undefined,
          apellidoMaterno: apellidoMaterno().trim() || undefined,
          ciNumero: ciNumero().trim(),
          ciComplemento: ciComplemento().trim() || undefined,
          ciExtension: ciExtension() || undefined,
          telefono: telefono().trim() || undefined,
          email: email().trim() || undefined,
          direccion: direccion().trim() || undefined,
        };

        await ApiClientGateway.crearClienteNatural(payload);
      } else {
        const payload: CrearClienteJuridicaPayload = {
          razonSocial: razonSocial().trim(),
          nit: nit().trim(),
          representante: hasRep()
            ? {
                nombres: repNombres().trim(),
                apellidoPaterno: repPaterno().trim() || undefined,
                apellidoMaterno: repMaterno().trim() || undefined,
                ciNumero: repCi().trim(),
                ciExtension: repExt() || undefined,
              }
            : undefined,
          telefono: telefono().trim() || undefined,
          email: email().trim() || undefined,
          direccion: direccion().trim() || undefined,
        };

        await ApiClientGateway.crearClienteJuridica(payload);
      }

      resetForm();
      props.onSuccess();
      props.onClose();
    } catch (err: any) {
      setError(err.message || 'Error al registrar el cliente');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Show when={props.open}>
      <div
        style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(15, 23, 18, 0.45)',
          'backdrop-filter': 'blur(4px)',
          display: 'flex',
          'align-items': 'center',
          'justify-content': 'center',
          'z-index': 1000,
          padding: '20px',
        }}
      >
        <div
          class="card animate-fade-in"
          style={{
            width: '100%',
            'max-width': '620px',
            'max-height': '90vh',
            'overflow-y': 'auto',
            background: 'var(--surface)',
            padding: '28px',
            'border-radius': 'var(--radius-l)',
            'box-shadow': '0 20px 40px rgba(0, 0, 0, 0.15)',
          }}
        >
          <div style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'margin-bottom': '16px' }}>
            <div>
              <h2 style={{ 'font-family': 'var(--font-display)', 'font-size': '20px', margin: 0, color: 'var(--ink)' }}>
                Registrar Nuevo Cliente (F6)
              </h2>
              <p style={{ 'font-size': '12.5px', color: 'var(--ink-soft)', margin: '4px 0 0' }}>
                Gestión de clientes según RN18 (Persona Natural o Jurídica).
              </p>
            </div>
            <button
              onClick={() => {
                resetForm();
                props.onClose();
              }}
              class="btn btn-ghost"
              style={{ padding: '6px 12px', 'font-size': '12px' }}
            >
              ✕
            </button>
          </div>

          <Show when={error()}>
            <div class="alert-error" style={{ 'margin-bottom': '16px' }}>
              {error()}
            </div>
          </Show>

          {/* Tipo Selector */}
          <div style={{ display: 'flex', gap: '10px', 'margin-bottom': '16px' }}>
            <button
              type="button"
              class={`btn ${tipo() === 'Natural' ? 'btn-primary' : 'btn-ghost'}`}
              style={{ flex: 1 }}
              onClick={() => setTipo('Natural')}
            >
              👤 Persona Natural
            </button>
            <button
              type="button"
              class={`btn ${tipo() === 'Juridica' ? 'btn-primary' : 'btn-ghost'}`}
              style={{ flex: 1 }}
              onClick={() => setTipo('Juridica')}
            >
              🏢 Persona Jurídica (Empresa)
            </button>
          </div>

          <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
            {/* Persona Natural Fields */}
            <Show when={tipo() === 'Natural'}>
              <div class="field">
                <label>Nombres *</label>
                <input
                  type="text"
                  placeholder="Ej. Carlos Alberto"
                  value={nombres()}
                  onInput={(e) => setNombres(e.currentTarget.value)}
                  required
                />
              </div>

              <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '12px' }}>
                <div class="field">
                  <label>Apellido Paterno</label>
                  <input
                    type="text"
                    placeholder="Ej. Mendoza"
                    value={apellidoPaterno()}
                    onInput={(e) => setApellidoPaterno(e.currentTarget.value)}
                  />
                </div>
                <div class="field">
                  <label>Apellido Materno</label>
                  <input
                    type="text"
                    placeholder="Ej. Vaca"
                    value={apellidoMaterno()}
                    onInput={(e) => setApellidoMaterno(e.currentTarget.value)}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', 'grid-template-columns': '2fr 1fr 1fr', gap: '12px' }}>
                <div class="field">
                  <label>Cédula de Identidad (CI) *</label>
                  <input
                    type="text"
                    placeholder="Solo dígitos"
                    value={ciNumero()}
                    onInput={(e) => setCiNumero(e.currentTarget.value)}
                    required
                  />
                </div>
                <div class="field">
                  <label>Compl.</label>
                  <input
                    type="text"
                    placeholder="Ej. 1A"
                    value={ciComplemento()}
                    onInput={(e) => setCiComplemento(e.currentTarget.value)}
                  />
                </div>
                <div class="field">
                  <label>Extensión</label>
                  <select
                    value={ciExtension()}
                    onChange={(e) => setCiExtension(e.currentTarget.value)}
                  >
                    <option value="CB">CB</option>
                    <option value="LP">LP</option>
                    <option value="SC">SC</option>
                    <option value="OR">OR</option>
                    <option value="PT">PT</option>
                    <option value="TJ">TJ</option>
                    <option value="CH">CH</option>
                    <option value="BE">BE</option>
                    <option value="PD">PD</option>
                  </select>
                </div>
              </div>
            </Show>

            {/* Persona Jurídica Fields */}
            <Show when={tipo() === 'Juridica'}>
              <div class="field">
                <label>Razón Social *</label>
                <input
                  type="text"
                  placeholder="Ej. Agroforestal del Valle S.R.L."
                  value={razonSocial()}
                  onInput={(e) => setRazonSocial(e.currentTarget.value)}
                  required
                />
              </div>

              <div class="field">
                <label>Número de Identificación Tributaria (NIT) *</label>
                <input
                  type="text"
                  placeholder="Solo dígitos numéricos"
                  value={nit()}
                  onInput={(e) => setNit(e.currentTarget.value)}
                  required
                />
              </div>

              <div style={{ 'margin-top': '4px' }}>
                <label style={{ display: 'flex', 'align-items': 'center', gap: '8px', cursor: 'pointer', 'font-size': '13px' }}>
                  <input
                    type="checkbox"
                    checked={hasRep()}
                    onChange={(e) => setHasRep(e.currentTarget.checked)}
                  />
                  <span>Asignar Representante Legal</span>
                </label>
              </div>

              <Show when={hasRep()}>
                <div style={{ padding: '12px', background: 'var(--surface-sunken)', 'border-radius': 'var(--radius-m)', display: 'flex', 'flex-direction': 'column', gap: '10px' }}>
                  <div class="field">
                    <label>Nombres Representante *</label>
                    <input
                      type="text"
                      value={repNombres()}
                      onInput={(e) => setRepNombres(e.currentTarget.value)}
                      required={hasRep()}
                    />
                  </div>
                  <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '10px' }}>
                    <div class="field">
                      <label>Apellido Paterno</label>
                      <input
                        type="text"
                        value={repPaterno()}
                        onInput={(e) => setRepPaterno(e.currentTarget.value)}
                      />
                    </div>
                    <div class="field">
                      <label>CI Representante *</label>
                      <input
                        type="text"
                        value={repCi()}
                        onInput={(e) => setRepCi(e.currentTarget.value)}
                        required={hasRep()}
                      />
                    </div>
                  </div>
                </div>
              </Show>
            </Show>

            {/* Common Contact Fields */}
            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '12px' }}>
              <div class="field">
                <label>Teléfono / Celular</label>
                <input
                  type="text"
                  placeholder="Ej. +591 76543210"
                  value={telefono()}
                  onInput={(e) => setTelefono(e.currentTarget.value)}
                />
              </div>
              <div class="field">
                <label>Correo Electrónico</label>
                <input
                  type="email"
                  placeholder="Ej. contacto@empresa.com"
                  value={email()}
                  onInput={(e) => setEmail(e.currentTarget.value)}
                />
              </div>
            </div>

            <div class="field">
              <label>Dirección / Ciudad</label>
              <input
                type="text"
                placeholder="Ej. Av. Blanco Galindo Km 5, Cochabamba"
                value={direccion()}
                onInput={(e) => setDireccion(e.currentTarget.value)}
              />
            </div>

            <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '10px', 'margin-top': '8px' }}>
              <button
                type="button"
                onClick={() => {
                  resetForm();
                  props.onClose();
                }}
                class="btn btn-ghost"
                disabled={submitting()}
              >
                Cancelar
              </button>
              <button
                type="submit"
                class="btn btn-primary"
                disabled={submitting()}
              >
                {submitting() ? 'Guardando...' : 'Registrar Cliente'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Show>
  );
}
