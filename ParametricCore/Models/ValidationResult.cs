namespace StructFlow.ParametricCore.Models
{
    /// <summary>
    /// ParameterValidator가 반환하는 유효성 검사 결과.
    /// 실패 시 모든 오류 목록을 포함한다.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public IReadOnlyList<string> Errors { get; private set; }

        private ValidationResult(bool isValid, IReadOnlyList<string> errors)
        {
            IsValid = isValid;
            Errors = errors;
        }

        public static ValidationResult Success() =>
            new(true, Array.Empty<string>());

        public static ValidationResult Failure(IEnumerable<string> errors) =>
            new(false, errors.ToList().AsReadOnly());

        public static ValidationResult Failure(string error) =>
            Failure(new[] { error });

        /// <summary>첫 번째 오류 메시지. 오류 없으면 null 반환.</summary>
        public string? FirstError => Errors.Count > 0 ? Errors[0] : null;

        public override string ToString() =>
            IsValid ? "VALID" : $"INVALID: {string.Join(" | ", Errors)}";
    }
}
