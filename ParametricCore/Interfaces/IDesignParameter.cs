namespace StructFlow.ParametricCore.Interfaces
{
    /// <summary>
    /// 모든 설계 파라미터 도메인이 구현해야 하는 공통 인터페이스.
    /// 도메인 교체 시 이 인터페이스만 유지하면 파이프라인은 그대로 동작한다.
    /// </summary>
    public interface IDesignParameter
    {
        /// <summary>설계 항목의 고유 식별자</summary>
        string Id { get; }

        /// <summary>파라미터 유효성 자가 검사</summary>
        bool IsValid(out string reason);

        /// <summary>기본값으로 초기화된 복사본 반환 (모호한 입력 보수적 처리용)</summary>
        IDesignParameter WithConservativeDefaults();
    }
}
