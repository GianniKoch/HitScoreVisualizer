using HitScoreVisualizer.UI;
using SiraUtil;
using Zenject;
using Utilities = SiraUtil.Utilities;

namespace HitScoreVisualizer.Installers
{
	public class MenuInstaller : Installer<MenuInstaller>
	{
		public override void InstallBindings()
		{
			Plugin.LoggerInstance.Debug($"Running {nameof(InstallBindings)} of {nameof(MenuInstaller)}");

			Container.Bind<ConfigSelectorViewController>().FromNewComponentOnNewGameObject().AsSingle().OnInstantiated(Utilities.SetupViewController);
			Container.Bind<HitScoreFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();

			Plugin.LoggerInstance.Debug($"Binding {nameof(SettingsControllerManager)}");
			Container.BindInterfacesAndSelfTo<SettingsControllerManager>().AsSingle().NonLazy();

			Plugin.LoggerInstance.Debug($"All bindings installed in {nameof(MenuInstaller)}");
		}
	}
}