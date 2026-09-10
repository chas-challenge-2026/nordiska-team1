import capitalize from "../utils/capitalize"

type InputFieldProps = {
    name: string;
    type: React.HTMLInputTypeAttribute;
    label: string;
    placeholder: string;
    value: string;
    required?: boolean;
    onChange: (value: string) => void;
    error?: string;
    suffix?: string;
}

/**
 * Reusable input field with a label, placeholder, error message, and support for required fields.
 * @param props - Input field configuration and state handling.
 * @returns A reusable input field.
 */
export default function InputField({
        name,
        type,
        label, 
        placeholder, 
        value,
        required = false,
        onChange,
        error,
        suffix,
    }: InputFieldProps) {

    return (
        <div>
            <div className="flex items-center justify-between">
                <label htmlFor={name} className="text-sm font-bold text-dark-navy">
                    {capitalize(label)} {required && <span className="text-red-600 font-light"> *</span>}
                </label>
                {error && (
                    <span id={`${name}-error`} className="text-sm text-[#C4291C]">{capitalize(error)}</span>
                )}
            </div>

            <div className="relative mt-1">
                <input
                    id = {name}
                    name = {name}
                    type = {type}
                    placeholder = {capitalize(placeholder)}
                    value = {value}
                    required = {required}
                    onChange={(e) => onChange(e.target.value)}
                    aria-invalid={!!error}
                    aria-describedby={error ? `${name}-error` : undefined}
                    className={`w-full rounded-md border ${error ? "border-[#C4291C]" : "border-nordiska-blue"} px-3 py-2 ${suffix ? "pr-12" : ""} placeholder:text-gray-400`}
                    />
                {suffix && (
                    <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-secondary">
                        {suffix}
                    </span>
                )}
            </div>
        </div>
    );
};
