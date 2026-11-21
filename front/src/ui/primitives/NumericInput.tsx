import { createSignal, createEffect, type JSX } from 'solid-js';

export interface NumericInputProps {
  value: number | null;
  onChange: (value: number | null) => void;
  step?: number; // default 1
  min?: number;
  max?: number;
  decimals?: number; // default 2
  unit?: string;
  placeholder?: string;
  disabled?: boolean;
  class?: string;
  id?: string;
}

export function NumericInput(props: NumericInputProps): JSX.Element {
  const [rawText, setRawText] = createSignal<string>(
    props.value === null || props.value === undefined ? '' : String(props.value)
  );

  // Synchronize external value changes to rawText
  createEffect(() => {
    const val = props.value;
    if (val === null || val === undefined) {
      if (rawText() !== '') {
        setRawText('');
      }
    } else {
      const currentParsed = parseFloat(rawText().replace(',', '.'));
      if (isNaN(currentParsed) || currentParsed !== val) {
        setRawText(String(val));
      }
    }
  });

  const handleInput = (e: InputEvent & { currentTarget: HTMLInputElement }) => {
    const inputVal = e.currentTarget.value.replace(',', '.');

    if (inputVal === '') {
      setRawText('');
      props.onChange(null);
      return;
    }

    const decimalsLimit = props.decimals ?? 2;
    const allowNegative = props.min === undefined || props.min < 0;

    const pattern =
      decimalsLimit === 0
        ? allowNegative
          ? /^-?\d*$/
          : /^\d*$/
        : allowNegative
          ? new RegExp(`^-?\\d*\\.?\\d{0,${decimalsLimit}}$`)
          : new RegExp(`^\\d*\\.?\\d{0,${decimalsLimit}}$`);

    if (pattern.test(inputVal)) {
      setRawText(inputVal);
      const parsed = parseFloat(inputVal);
      if (!isNaN(parsed) && !inputVal.endsWith('.') && inputVal !== '-') {
        props.onChange(parsed);
      }
    } else {
      // Revert if pattern fails
      e.currentTarget.value = rawText();
    }
  };

  const handleBlur = () => {
    const text = rawText().trim().replace(',', '.');

    if (text === '' || text === '.' || text === '-') {
      setRawText('');
      props.onChange(null);
      return;
    }

    let parsed = parseFloat(text);
    if (isNaN(parsed)) {
      setRawText('');
      props.onChange(null);
      return;
    }

    // Clamp to min/max on blur
    if (props.min !== undefined && parsed < props.min) {
      parsed = props.min;
    }
    if (props.max !== undefined && parsed > props.max) {
      parsed = props.max;
    }

    setRawText(String(parsed));
    props.onChange(parsed);
  };

  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
      e.preventDefault();
    }
  };

  const handleWheel = (e: WheelEvent) => {
    (e.currentTarget as HTMLElement).blur();
  };

  if (props.unit) {
    return (
      <div class={`numeric-input ${props.class ?? ''}`}>
        <input
          id={props.id}
          type="text"
          inputMode="decimal"
          autocomplete="off"
          class="numeric-input__field"
          value={rawText()}
          placeholder={props.placeholder}
          disabled={props.disabled}
          onInput={handleInput}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          onWheel={handleWheel}
        />
        <span class="numeric-input__unit">{props.unit}</span>
      </div>
    );
  }

  return (
    <input
      id={props.id}
      type="text"
      inputMode="decimal"
      autocomplete="off"
      class={`numeric-input__field ${props.class ?? ''}`}
      value={rawText()}
      placeholder={props.placeholder}
      disabled={props.disabled}
      onInput={handleInput}
      onBlur={handleBlur}
      onKeyDown={handleKeyDown}
      onWheel={handleWheel}
    />
  );
}
