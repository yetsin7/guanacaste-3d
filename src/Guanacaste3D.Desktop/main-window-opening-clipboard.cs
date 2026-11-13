using System.IO;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Copies or cuts only the selected hosted opening, retaining its type and orientation.</summary>
    private void CopySelectedOpening(Guid id, bool cut)
    {
        var opening = _session.Document.Openings.FirstOrDefault(item => item.Id == id);
        if (opening is null) return;
        try
        {
            if (cut)
            {
                _session.RemoveOpening(id);
                _selectedOpeningId = null;
                _selectedWallOffset = null;
                RefreshAll();
            }
            _openingClipboard = opening;
            _wallClipboard = null;
            StatusText.Text = GetText(cut ? "Status.OpeningCut" : "Status.OpeningCopied");
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
        }
    }

    /// <summary>Pastes onto the selected host, or the original host, in the nearest available gap.</summary>
    private void PasteOpeningClipboard(WallOpening opening, string successKey = "Status.OpeningPasted")
    {
        var hostId = _selectedWallId ?? opening.WallId;
        var wall = _session.Document.Walls.FirstOrDefault(item => item.Id == hostId);
        if (wall is null || FindOpeningPasteOffset(wall, opening) is not double offset)
        {
            StatusText.Text = GetText("Status.OpeningPasteUnavailable");
            return;
        }
        try
        {
            var id = _session.AddOpening(hostId, opening.Kind, offset, opening.Width, opening.Sill, opening.Height,
                opening.OpeningTypeId, opening.FlipHand, opening.FlipFacing);
            _selectedWallId = hostId;
            _selectedOpeningId = id;
            _selectedWallOffset = offset;
            RefreshAll();
            StatusText.Text = GetText(successKey);
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
        }
    }

    /// <summary>Finds a host interval with the required end and opening clearances without mutating history.</summary>
    private double? FindOpeningPasteOffset(Wall wall, WallOpening opening)
    {
        const double clearance = 0.050001;
        var hosted = _session.Document.Openings.Where(item => item.WallId == wall.Id).OrderBy(item => item.Begin).ToArray();
        var candidates = new List<double>();
        var gapStart = clearance;
        foreach (var item in hosted)
        {
            AddCandidate(item.Begin - clearance);
            gapStart = item.End + clearance;
        }
        AddCandidate(wall.Length - clearance);
        return candidates.OrderBy(candidate => Math.Abs(candidate - opening.Offset)).Cast<double?>().FirstOrDefault();

        /// <summary>Checks the closest position in a free interval against the host geometry.</summary>
        void AddCandidate(double gapEnd)
        {
            var minimum = gapStart + opening.Width / 2;
            var maximum = gapEnd - opening.Width / 2;
            if (minimum > maximum) return;
            var candidate = Math.Clamp(opening.Offset, minimum, maximum);
            try
            {
                var proposed = opening with { Id = Guid.NewGuid(), WallId = wall.Id, Offset = candidate };
                WallGeometry.Validate([wall], [.. hosted, proposed]);
                candidates.Add(candidate);
            }
            catch (InvalidDataException) { }
        }
    }
}
