import { Option } from "./Option";
import { VisibleIf } from "./VisibleIf";

export interface QuestionDefinition {
    id: string;
    key: string;
    label: string;
    type: string;

    required?: boolean;
    placeholder?: string;
    defaultValue?: string | number | null;

    options?: Option[];
    /** The statements of a likert grid, each rated on the scale in `options`. */
    rows?: Option[];
    visibleIf?: VisibleIf;

    validationConfigs?: string | null;

    helpText?: string;
}