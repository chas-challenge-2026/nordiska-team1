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
    }: InputFieldProps) {

    return (
        <div>
            <div className="flex items-center justify-between">
                <label htmlFor={name} className="text-sm font-bold text-dark-navy">
                    {capitalize(label)} {required && <span className="text-red-600 font-light"> *</span>}
                </label>
                {error && (
                    <span className="text-sm text-red-600">{capitalize(error)}</span>
                )}
            </div>

            <input 
                id = {name}
                name = {name}
                type = {type}
                placeholder = {capitalize(placeholder)}
                value = {value}
                required = {required}
                onChange={(e) => onChange(e.target.value)}
                className="mt-1 w-full rounded-md border border-nordiska-blue px-3 py-2 placeholder:text-gray-400"
                />    
        </div>
    );
};