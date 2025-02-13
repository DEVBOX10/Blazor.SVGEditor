﻿using Microsoft.AspNetCore.Components.Web;

namespace KristofferStrube.Blazor.SVGEditor;

public partial class SVGEditor
{
    private void Move(PointerEventArgs eventArgs)
    {
        if (TranslatePanner.HasValue)
        {
            (double x, double y) newPanner = (x: eventArgs.OffsetX, y: eventArgs.OffsetY);
            Translate = (Translate.x + newPanner.x - TranslatePanner.Value.x, Translate.y + newPanner.y - TranslatePanner.Value.y);
            TranslatePanner = newPanner;
        }
        else
        {
            (double x, double y) = LocalDetransform((eventArgs.OffsetX, eventArgs.OffsetY));
            if (EditGradient is LinearGradient linearGradient && linearGradient.CurrentStop is int stop)
            {
                (double x, double y) boundingBox = (x: linearGradient.EditingShape!.BoundingBox.X, y: linearGradient.EditingShape.BoundingBox.Y);
                (double x, double y) external = Sub((x, y), boundingBox);
                if (stop is -1)
                {
                    (linearGradient.X1, linearGradient.Y1) = (external.x / linearGradient.EditingShape.BoundingBox.Width, external.y / linearGradient.EditingShape.BoundingBox.Height);
                }
                else if (stop is -2)
                {
                    (linearGradient.X2, linearGradient.Y2) = (external.x / linearGradient.EditingShape.BoundingBox.Width, external.y / linearGradient.EditingShape.BoundingBox.Height);
                }
                else
                {
                    // Used the folloing pseudocode supplied by FunByJohn: https://pastebin.com/fG0CH4Wv
                    (double x, double y) p1 = (x: linearGradient.X1 * linearGradient.EditingShape.BoundingBox.Width, y: linearGradient.Y1 * linearGradient.EditingShape.BoundingBox.Height);
                    (double x, double y) p2 = (x: linearGradient.X2 * linearGradient.EditingShape.BoundingBox.Width, y: linearGradient.Y2 * linearGradient.EditingShape.BoundingBox.Height);
                    (double x, double y) v = Sub(p2, p1);
                    (double x, double y) externalPrime = Sub(external, p1);
                    double scalar = ProjectScalar(externalPrime, v);
                    linearGradient.Stops[stop].Offset = Math.Clamp(scalar, 0, 1);
                    if (stop is not 0 && linearGradient.Stops[stop - 1].Offset > linearGradient.Stops[stop].Offset)
                    {
                        (linearGradient.Stops[stop - 1], linearGradient.Stops[stop]) = (linearGradient.Stops[stop], linearGradient.Stops[stop - 1]);
                        _ = linearGradient.Element.InsertBefore(linearGradient.Element.RemoveChild(linearGradient.Stops[stop - 1].Element), linearGradient.Stops[stop].Element);
                        linearGradient.CurrentStop--;
                    }
                    else if (linearGradient.Stops.Count is > 1 && stop != linearGradient.Stops.Count - 1 && linearGradient.Stops[stop].Offset > linearGradient.Stops[stop + 1].Offset)
                    {
                        (linearGradient.Stops[stop + 1], linearGradient.Stops[stop]) = (linearGradient.Stops[stop], linearGradient.Stops[stop + 1]);
                        _ = linearGradient.Element.InsertBefore(linearGradient.Element.RemoveChild(linearGradient.Stops[stop].Element), linearGradient.Stops[stop + 1].Element);
                        linearGradient.CurrentStop++;
                    }
                }
            }
            else if (CurrentEditShape is Shape shape)
            {
                shape.HandlePointerMove(eventArgs);
            }
            else
            {
                if (MarkedShapes.Count == 0 && SelectionBox is not null)
                {
                    SelectionBox.Width = x - SelectionBox.X;
                    SelectionBox.Height = y - SelectionBox.Y;
                    BoxSelectionShapes = SelectionMode switch
                    {
                        SelectionMode.WindowSelection => WindowSelection(SelectionBox),
                        SelectionMode.CrossingSelection => CrossingSelection(SelectionBox),
                        _ => CrossingSelection(SelectionBox)
                    };
                }
                else
                {
                    MarkedShapes.ForEach(e => e.HandlePointerMove(eventArgs));
                    MovePanner = (x, y);
                }
            }
        }
    }

    private static (double x, double y) Sub((double x, double y) u, (double x, double y) v)
    {
        return (u.x - v.x, u.y - v.y);
    }

    private static double Dot((double x, double y) u, (double x, double y) v)
    {
        return (u.x * v.x) + (u.y * v.y);
    }

    private static double ProjectScalar((double x, double y) u, (double x, double y) v)
    {
        return Dot(u, v) / Dot(v, v);
    }

    public void Down(PointerEventArgs eventArgs)
    {
        if (eventArgs.Button == 1)
        {
            if (DisablePanning)
            {
                return;
            }

            TranslatePanner = (eventArgs.OffsetX, eventArgs.OffsetY);
        }
        else
        {
            if (DisableBoxSelection)
            {
                return;
            }

            (double x, double y) = LocalDetransform((eventArgs.OffsetX, eventArgs.OffsetY));
            SelectionBox = new Box() { X = x, Y = y };
        }
    }

    public void Up(PointerEventArgs eventArgs)
    {
        MoveOverShapes.Clear();
        CurrentEditShape = null;
        if (BoxSelectionShapes is { Count: > 0 })
        {
            SelectedShapes = BoxSelectionShapes;
            UnfocusShape();
        }
        BoxSelectionShapes = null;
        SelectionBox = null;
        if (EditGradient is LinearGradient linearGradient)
        {
            linearGradient.CurrentStop = null;
        }
        SelectedShapes.ForEach(e => e.HandlePointerUp(eventArgs));
        if (eventArgs.Button == 2 || eventArgs.Type == "contextmenu")
        {
            LastRightClick = (eventArgs.OffsetX, eventArgs.OffsetY);
        }
        else if (eventArgs.Button == 1)
        {
            TranslatePanner = null;
            ClearSelectedShapes();
        }
        if (EditMode is EditMode.Move)
        {
            EditMode = EditMode.None;
        }
    }

    public void UnSelect(MouseEventArgs eventArgs)
    {
        if (EditMode != EditMode.Add && !eventArgs.CtrlKey)
        {
            EditMode = EditMode.None;
            ClearSelectedShapes();
            UnfocusShape();
            EditGradient = null;
        }
    }

    public void Out(PointerEventArgs eventArgs)
    {
        SelectedShapes.ForEach(e => e.HandlePointerOut(eventArgs));
    }

    public void Wheel(WheelEventArgs eventArgs)
    {
        if (eventArgs.DeltaY < 0)
        {
            ZoomIn(eventArgs.OffsetX, eventArgs.OffsetY);
        }
        else if (eventArgs.DeltaY > 0)
        {
            ZoomOut(eventArgs.OffsetX, eventArgs.OffsetY);
        }
    }

    private List<Shape> WindowSelection(Box box)
    {
        return Elements.Where(e => e is Shape).Select(e => (Shape)e).Where(s => s.SelectionPoints.All(p => PointWitinBox(p, box))).ToList();
    }

    private List<Shape> CrossingSelection(Box box)
    {
        return Elements.Where(e => e is Shape).Select(e => (Shape)e).Where(s => s.SelectionPoints.Any(p => PointWitinBox(p, box))).ToList();
    }

    private static bool PointWitinBox((double x, double y) point, Box box)
    {
        return (box.Width, box.Height) switch
        {
            ( >= 0, >= 0) => point.x >= box.X && point.y >= box.Y & point.x <= box.X + box.Width && point.y <= box.Y + box.Height,
            ( >= 0, < 0) => point.x >= box.X && point.y <= box.Y & point.x <= box.X + box.Width && point.y >= box.Y + box.Height,
            ( < 0, >= 0) => point.x <= box.X && point.y >= box.Y & point.x >= box.X + box.Width && point.y <= box.Y + box.Height,
            _ => point.x <= box.X && point.y <= box.Y & point.x >= box.X + box.Width && point.y >= box.Y + box.Height,
        };
    }
}
