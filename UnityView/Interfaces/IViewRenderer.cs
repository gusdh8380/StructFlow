using StructFlow.SimulationEngine;

namespace StructFlow.UnityView.Interfaces
{
    /// <summary>
    /// 모든 뷰 렌더러가 구현해야 하는 공통 인터페이스.
    /// 계산 로직은 포함하지 않으며, SimulationResult를 시각화하는 역할만 담당한다.
    /// 도메인 교체 시 이 인터페이스 구현체만 교체한다.
    /// </summary>
    public interface IViewRenderer
    {
        /// <summary>시뮬레이션 결과로 씬을 업데이트한다</summary>
        void Render(SimulationResult result);

        /// <summary>씬에서 렌더링된 객체를 제거한다</summary>
        void Clear();
    }
}
