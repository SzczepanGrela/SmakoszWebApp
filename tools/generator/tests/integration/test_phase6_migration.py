from unittest.mock import MagicMock, patch

from generators.phase6_social import SocialGraphPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase6Metadata:

    def test_social_graph_phase_metadata(self):
        phase = SocialGraphPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase6_social"
        assert metadata.display_name == "Social Graph Generation"

        assert len(metadata.dependencies) == 2
        assert "phase4_users" in metadata.dependencies
        assert "phase5_reviews" in metadata.dependencies

        assert "user_follows" in metadata.required_tables
        assert "review_likes" in metadata.required_tables
        assert "notifications" in metadata.required_tables
        assert "search_histories" in metadata.required_tables
        assert "favorite_restaurants" in metadata.required_tables
        assert "data_correction_requests" in metadata.required_tables
        assert "reports" in metadata.required_tables
        assert "report_reason_assignments" in metadata.required_tables

class TestPhase6Registration:

    def test_social_graph_phase_registers(self):
        registry = PhaseRegistry()
        phase = SocialGraphPhase()

        registry.register(phase)

        retrieved = registry.get("phase6_social")
        assert retrieved is phase

    def test_phase6_dependency_resolution(self):
        from generators.phase1_definitions import CitiesPhase, IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase
        from generators.phase5_reviews import ReviewsPhase

        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(IngredientsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())
        registry.register(SocialGraphPhase())

        resolved = registry.resolve_dependencies(["phase6_social"])

        assert "phase4_users" in resolved
        assert "phase5_reviews" in resolved
        assert "phase6_social" in resolved

        users_idx = resolved.index("phase4_users")
        reviews_idx = resolved.index("phase5_reviews")
        social_idx = resolved.index("phase6_social")

        assert users_idx < social_idx
        assert reviews_idx < social_idx

class TestPhase6DependencyValidation:

    def test_phase6_requires_users(self):
        mock_db = MagicMock()

        phase = SocialGraphPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase6_social.generate_social_graph") as mock_gen:
            mock_gen.side_effect = Exception("No users found")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "users" in str(result.error).lower()

class TestPhase6ExecutionStructure:

    def test_social_graph_phase_execute_structure(self):
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            5000,
            25000,
            15000,
            8000,
            2000,
            200,
            150,
        ]

        phase = SocialGraphPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase6_social.generate_social_graph"):
            result = phase.execute(context)

        assert result.phase_id == "phase6_social"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        assert "user_follows" in result.entities_generated
        assert "review_likes" in result.entities_generated
        assert "notifications" in result.entities_generated
        assert "search_histories" in result.entities_generated
        assert "favorite_restaurants" in result.entities_generated
        assert "data_correction_requests" in result.entities_generated
        assert "reports" in result.entities_generated

        assert result.entities_generated["user_follows"] == 5000
        assert result.entities_generated["review_likes"] == 25000
        assert result.entities_generated["notifications"] == 15000

        assert result.error is None

class TestPhase6ErrorHandling:

    def test_social_graph_phase_handles_generation_failure(self):
        mock_db = MagicMock()

        phase = SocialGraphPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase6_social.generate_social_graph") as mock_gen:
            mock_gen.side_effect = RuntimeError("Worker pool initialization failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "worker" in str(result.error).lower()

class TestPhase6CompleteChain:

    def test_full_pipeline_with_phase6(self):
        from generators.phase1_definitions import CitiesPhase, IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase
        from generators.phase5_reviews import ReviewsPhase

        registry = PhaseRegistry()

        from generators.phase0_config import SystemConfigPhase

        registry.register(SystemConfigPhase())
        registry.register(CitiesPhase())
        registry.register(IngredientsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())
        registry.register(SocialGraphPhase())

        resolved = registry.resolve_dependencies(["phase6_social"])

        assert "phase1_cities" in resolved
        assert "phase4_users" in resolved
        assert "phase5_reviews" in resolved
        assert "phase6_social" in resolved

        users_idx = resolved.index("phase4_users")
        reviews_idx = resolved.index("phase5_reviews")
        social_idx = resolved.index("phase6_social")

        assert users_idx < social_idx
        assert reviews_idx < social_idx
