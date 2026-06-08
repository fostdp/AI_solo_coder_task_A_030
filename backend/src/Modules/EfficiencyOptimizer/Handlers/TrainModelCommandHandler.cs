using MediatR;
using ChillerPlantOptimization.Contracts.Commands;

namespace ChillerPlantOptimization.Modules.EfficiencyOptimizer.Handlers;

public class TrainModelCommandHandler : IRequestHandler<TrainModelCommand, bool>
{
    private readonly IEfficiencyOptimizer _optimizer;
    private readonly ILogger<TrainModelCommandHandler> _logger;

    public TrainModelCommandHandler(IEfficiencyOptimizer optimizer, ILogger<TrainModelCommandHandler> logger)
    {
        _optimizer = optimizer;
        _logger = logger;
    }

    public async Task<bool> Handle(TrainModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _optimizer.TrainModelAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "训练模型失败");
            return false;
        }
    }
}
