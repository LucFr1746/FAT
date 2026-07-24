using Data;
using Microsoft.EntityFrameworkCore;

namespace Services.Implementations;

/// <summary>
/// Keeps Major.RequiredCredits and Major.TotalTerms equal to the curriculum
/// they summarise.
///
/// THIS IS NOT A CONVENIENCE. RequiredCredits is the DENOMINATOR of every
/// student's graduation percentage. Let it drift away from the sum of the
/// curriculum's credits and the progress bar is wrong for every student in the
/// programme - and nothing in the UI reveals why. db/02_seed_master.sql asserts
/// the two agree, so a drifted value also breaks a fresh database build.
///
/// Shared by the import and by the curriculum admin service so both cannot
/// disagree about how the total is computed.
/// </summary>
internal static class MajorCreditCalculator
{
    /// <summary>
    /// Applies a change and then brings the affected majors' totals back into
    /// line, atomically.
    ///
    /// THE ORDER MATTERS AND IS EASY TO GET WRONG. <see cref="SyncAsync"/>
    /// computes the total with a database query, so it cannot see rows that are
    /// still only in the change tracker. Calling it BEFORE saving therefore sums
    /// the OLD curriculum and writes a stale RequiredCredits - the exact drift
    /// this class exists to prevent, and invisible in the UI.
    ///
    /// So: save the change, then recompute, then save the totals - with both
    /// saves inside one transaction, because a curriculum that changed without
    /// its resync is the broken state.
    /// </summary>
    /// <param name="applyChange">Stages the change; must NOT call SaveChanges.</param>
    /// <param name="getAffectedMajorIds">
    /// Runs after the change is saved, so it can see newly inserted rows.
    /// </param>
    public static async Task ApplyAndSyncAsync(
        FAT_DBContext db,
        Func<Task> applyChange,
        Func<Task<IReadOnlyList<int>>> getAffectedMajorIds,
        CancellationToken cancellationToken = default)
    {
        // The in-memory provider used by the unit tests has no transactions;
        // asking for one there would throw rather than protect anything.
        var useTransaction = db.Database.IsRelational();
        await using var transaction = useTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await applyChange();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var majorId in await getAffectedMajorIds())
        {
            await SyncAsync(db, majorId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Recomputes the totals for one major from its curriculum rows.
    ///
    /// Does NOT call SaveChanges, and reads the curriculum FROM THE DATABASE -
    /// so any pending change must already be saved. Prefer
    /// <see cref="ApplyAndSyncAsync"/>, which enforces that ordering.
    /// </summary>
    /// <returns>True when something actually changed.</returns>
    public static async Task<bool> SyncAsync(
        FAT_DBContext db,
        int majorId,
        CancellationToken cancellationToken = default)
    {
        var major = await db.Majors.FirstOrDefaultAsync(m => m.MajorId == majorId, cancellationToken);
        if (major is null)
        {
            return false;
        }

        var stats = await db.CurriculumItems
            .Where(ci => ci.MajorId == majorId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCredits = g.Sum(ci => ci.Course!.Credits),
                MaxTermNo = g.Max(ci => ci.TermNo)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // CK_Major_Credit demands RequiredCredits > 0 AND TotalTerms > 0, so an
        // empty curriculum has to fall back to 1 rather than 0 - otherwise
        // removing the last subject from a major fails on a constraint that has
        // nothing to do with what the user was doing.
        var requiredCredits = Math.Max(1, stats?.TotalCredits ?? 0);
        var totalTerms = Math.Max(1, stats?.MaxTermNo ?? 0);

        if (major.RequiredCredits == requiredCredits && major.TotalTerms == totalTerms)
        {
            return false;
        }

        major.RequiredCredits = requiredCredits;
        major.TotalTerms = totalTerms;
        return true;
    }
}
