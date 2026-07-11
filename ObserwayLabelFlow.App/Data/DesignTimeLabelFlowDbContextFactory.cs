using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ObserwayLabelFlow.App.Data;

public sealed class DesignTimeLabelFlowDbContextFactory : IDesignTimeDbContextFactory<LabelFlowDbContext>
{
    public LabelFlowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LabelFlowDbContext>()
            .UseSqlite("Data Source=labelflow.design.db")
            .Options;

        return new LabelFlowDbContext(options);
    }
}
