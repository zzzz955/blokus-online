namespace App.Audio
{
    /// <summary>
    /// 효과음(SFX) 타입 정의
    /// </summary>
    public enum SFXType
    {
        None = 0,

        // UI 인터랙션
        ButtonHover = 1,      // 버튼 호버링
        ButtonClick = 2,      // 버튼 클릭
        ModalOpen = 3,        // 모달 열기
        ModalClose = 4,       // 모달 닫기

        // 게임플레이
        BlockPlace = 5,       // 블록 배치
        TurnChange = 6,       // 턴 변경 (멀티플레이)
        CountDown = 7,        // 카운트다운 (5초 이하, 멀티플레이)
        TimeOut = 8,          // 시간 초과 (내 턴, 멀티플레이)

        // 게임 결과
        StageClear = 9,       // 클리어 / 우승
        StageFail = 10        // 실패 / 패배
    }
}
