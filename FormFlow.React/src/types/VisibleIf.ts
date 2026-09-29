/** Show a question only when the yes/no question `key` was answered with `shouldEqual`. */
export interface VisibleIf {
    key: string;
    shouldEqual: boolean;
}
