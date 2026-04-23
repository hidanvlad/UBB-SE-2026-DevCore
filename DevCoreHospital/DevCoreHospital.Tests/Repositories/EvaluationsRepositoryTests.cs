using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class EvaluationsRepositoryTests
{
    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsNotNumeric_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository().GetEvaluationsByDoctor("not-a-number"));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsEmpty_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository().GetEvaluationsByDoctor(string.Empty));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsWhitespace_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository().GetEvaluationsByDoctor("   "));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsAlphanumeric_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository().GetEvaluationsByDoctor("DR-42"));
}
