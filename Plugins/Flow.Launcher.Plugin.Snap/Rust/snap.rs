#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SnapZone {
    LeftHalf,
    RightHalf,
    TopHalf,
    BottomHalf,
    TopLeftQuarter,
    TopRightQuarter,
    BottomLeftQuarter,
    BottomRightQuarter,
    Center,
    Maximize,
    Minimize,
    LeftThird,
    CenterThird,
    RightThird,
    LeftTwoThirds,
    RightTwoThirds,
}

#[derive(Debug, Clone, Copy)]
pub struct Rect {
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

impl Rect {
    pub fn new(x: i32, y: i32, width: i32, height: i32) -> Self {
        Self { x, y, width, height }
    }
}

pub struct WindowSnapCalculator;

impl WindowSnapCalculator {
    /// Calculate the target window rectangle given work area bounds and desired snap zone
    pub fn calculate_target_rect(work_area: Rect, zone: SnapZone) -> Option<Rect> {
        let (wx, wy, ww, wh) = (work_area.x, work_area.y, work_area.width, work_area.height);
        let half_w = ww / 2;
        let half_h = wh / 2;
        let third_w = ww / 3;
        let two_thirds_w = (ww * 2) / 3;

        match zone {
            SnapZone::LeftHalf => Some(Rect::new(wx, wy, half_w, wh)),
            SnapZone::RightHalf => Some(Rect::new(wx + half_w, wy, ww - half_w, wh)),
            SnapZone::TopHalf => Some(Rect::new(wx, wy, ww, half_h)),
            SnapZone::BottomHalf => Some(Rect::new(wx, wy + half_h, ww, wh - half_h)),

            SnapZone::TopLeftQuarter => Some(Rect::new(wx, wy, half_w, half_h)),
            SnapZone::TopRightQuarter => Some(Rect::new(wx + half_w, wy, ww - half_w, half_h)),
            SnapZone::BottomLeftQuarter => Some(Rect::new(wx, wy + half_h, half_w, wh - half_h)),
            SnapZone::BottomRightQuarter => Some(Rect::new(wx + half_w, wy + half_h, ww - half_w, wh - half_h)),

            SnapZone::Center => {
                let cw = (ww * 3) / 4;
                let ch = (wh * 3) / 4;
                let cx = wx + (ww - cw) / 2;
                let cy = wy + (wh - ch) / 2;
                Some(Rect::new(cx, cy, cw, ch))
            }

            SnapZone::LeftThird => Some(Rect::new(wx, wy, third_w, wh)),
            SnapZone::CenterThird => Some(Rect::new(wx + third_w, wy, third_w, wh)),
            SnapZone::RightThird => Some(Rect::new(wx + ww - third_w, wy, third_w, wh)),

            SnapZone::LeftTwoThirds => Some(Rect::new(wx, wy, two_thirds_w, wh)),
            SnapZone::RightTwoThirds => Some(Rect::new(wx + ww - two_thirds_w, wy, two_thirds_w, wh)),

            SnapZone::Maximize | SnapZone::Minimize => None, // Handled via ShowWindow Win32 API
        }
    }
}
