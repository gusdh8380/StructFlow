using StructFlow.ParametricCore.Models;

namespace StructFlow.SimulationEngine.Interfaces
{
    /// <summary>
    /// 모든 시뮬레이션 엔진이 구현해야 하는 공통 인터페이스.
    /// 도메인(파이프 → 보 → 도로 등) 교체 시 이 인터페이스만 유지하면 된다.
    /// </summary>
    public interface ISimulationEngine
    {
        /// <summary>
        /// DesignSchema를 받아 시뮬레이션을 실행하고 SimulationResult를 반환한다.
        /// 내부 오류 발생 시 예외 대신 SimulationResult.OverallStatus = "ERROR"로 반환한다.
        /// </summary>
        SimulationResult Run(DesignSchema schema);
    }
}
