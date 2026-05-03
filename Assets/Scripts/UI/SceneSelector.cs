using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSelector : MonoBehaviour
{
    public void SelectScene(int sceneIndex)
    {

        switch (sceneIndex)
        {
            case 1:
                ViewerState.SetSceneName("Здоровый мозг");
                ViewerState.SetSceneDescription("На этой сцене показан здоровый мозг. Он состоит из двух полушарий, которые соединены мозолистым телом. Мозг контролирует все функции организма, включая движение, ощущения, мышление и эмоции. Здоровый мозг имеет гладкую поверхность с извилинами и бороздами, которые увеличивают его площадь.");
                break;
            case 2:
                ViewerState.SetSceneName("Мозг с опухолью");
                ViewerState.SetSceneDescription("На этой сцене показан мозг с опухолью. Опухоль расположена в правой височной доле и имеет размер около 3 см в диаметре. Она оказывает давление на окружающие ткани и может вызывать головные боли, судороги и другие симптомы.");
                break;
            case 3:
                ViewerState.SetSceneName("Импортированная модель");
                ViewerState.SetSceneDescription("На этой сцене показана модель, импортированная пользователем. Вы можете включать и выключать видимость отдельных слоёв модели через меню слоёв. Для взаимодействия с моделью используйте контроллеры или геймпад.");
                break;
        }

        // 1 is universal for now
        SceneManager.LoadSceneAsync(1);
    }

    public void ExitToMain()
    {
        SceneManager.LoadSceneAsync(0);
    }
}
