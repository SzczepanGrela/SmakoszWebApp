"""
Integration test for Phase 6 migration to new architecture.

Validates that SocialGraphPhase works correctly with user dependency
and integrates with PhaseRegistry.
"""

from unittest.mock import MagicMock, patch

from generators.phase6_social import SocialGraphPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase6Metadata:
    """Test SocialGraphPhase metadata."""

    def test_social_graph_phase_metadata(self):
        """Test SocialGraphPhase has correct metadata."""
        phase = SocialGraphPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase6_social"
        assert metadata.display_name == "Social Graph Generation"

        # Critical: Phase 6 depends only on Phase 4 (users)
        assert len(metadata.dependencies) == 1
        assert "phase4_users" in metadata.dependencies

        # Check all required tables
        assert "user_follows" in metadata.required_tables
        assert "review_likes" in metadata.required_tables
        assert "notifications" in metadata.required_tables
        assert "search_history" in metadata.required_tables
        assert "favorite_restaurants" in metadata.required_tables
        assert "data_correction_requests" in metadata.required_tables
        assert "reports" in metadata.required_tables
        assert "report_reason_assignments" in metadata.required_tables

class TestPhase6Registration:
    """Test Phase 6 integration with PhaseRegistry."""

    def test_social_graph_phase_registers(self):
        """Test that SocialGraphPhase can be registered."""
        registry = PhaseRegistry()
        phase = SocialGraphPhase()

        registry.register(phase)

        retrieved = registry.get("phase6_social")
        assert retrieved is phase

    def test_phase6_dependency_resolution(self):
        """Test that Phase 6 dependencies are resolved correctly."""
        from generators.phase1_definitions import CitiesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        # Register Phase 1 cities (required by Users) and Phase 4
        registry.register(CitiesPhase())
        registry.register(UsersPhase())
        registry.register(SocialGraphPhase())

        # Resolve dependencies for Phase 6
        resolved = registry.resolve_dependencies(["phase6_social"])

        # Should include cities, users, then social
        assert len(resolved) == 3
        assert "phase1_cities" in resolved
        assert "phase4_users" in resolved
        assert "phase6_social" in resolved

        # Verify order
        cities_idx = resolved.index("phase1_cities")
        users_idx = resolved.index("phase4_users")
        social_idx = resolved.index("phase6_social")

        assert cities_idx < users_idx
        assert users_idx < social_idx

class TestPhase6DependencyValidation:
    """Test Phase 6 dependency validation."""

    def test_phase6_requires_users(self):
        """Test that Phase 6 validates users dependency."""
        mock_db = MagicMock()

        phase = SocialGraphPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Simulate error due to missing users
        with patch("generators.phase6_social.generate_social_graph") as mock_gen:
            mock_gen.side_effect = Exception("No users found")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "users" in str(result.error).lower()

class TestPhase6ExecutionStructure:
    """Test Phase 6 execution structure."""

    def test_social_graph_phase_execute_structure(self):
        """Test that execute() returns proper PhaseResult structure."""
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            5000,   # user_follows count
            25000,  # review_likes count
            15000,  # notifications count
            8000,   # search_history count
            2000,   # favorite_restaurants count
            200,    # data_correction_requests count
            150,    # reports count
        ]

        phase = SocialGraphPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Mock the generate_social_graph function
        with patch("generators.phase6_social.generate_social_graph"):
            result = phase.execute(context)

        # Verify result structure
        assert result.phase_id == "phase6_social"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        # Check entities generated
        assert "user_follows" in result.entities_generated
        assert "review_likes" in result.entities_generated
        assert "notifications" in result.entities_generated
        assert "search_history" in result.entities_generated
        assert "favorite_restaurants" in result.entities_generated
        assert "data_correction_requests" in result.entities_generated
        assert "reports" in result.entities_generated

        assert result.entities_generated["user_follows"] == 5000
        assert result.entities_generated["review_likes"] == 25000
        assert result.entities_generated["notifications"] == 15000

        assert result.error is None

class TestPhase6ErrorHandling:
    """Test Phase 6 error handling."""

    def test_social_graph_phase_handles_generation_failure(self):
        """Test that SocialGraphPhase handles generation failures gracefully."""
        mock_db = MagicMock()

        phase = SocialGraphPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Mock generate_social_graph to raise an exception
        with patch("generators.phase6_social.generate_social_graph") as mock_gen:
            mock_gen.side_effect = RuntimeError("Worker pool initialization failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "worker" in str(result.error).lower()

class TestPhase6CompleteChain:
    """Test complete dependency chain including Phase 6."""

    def test_full_pipeline_with_phase6(self):
        """Test that Phase 6 can be resolved with full dependency chain."""
        from generators.phase1_definitions import CitiesPhase, IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase
        from generators.phase5_reviews import ReviewsPhase

        registry = PhaseRegistry()

        # Register all phases (0-6)
        from generators.phase0_config import SystemConfigPhase
        registry.register(SystemConfigPhase())
        registry.register(CitiesPhase())
        registry.register(IngredientsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())
        registry.register(SocialGraphPhase())

        # Request only Phase 6
        resolved = registry.resolve_dependencies(["phase6_social"])

        # Should automatically include all necessary dependencies
        # Phase 6 needs: Users (which needs Cities)
        assert "phase1_cities" in resolved
        assert "phase4_users" in resolved
        assert "phase6_social" in resolved

        # Cities -> Users -> Social
        cities_idx = resolved.index("phase1_cities")
        users_idx = resolved.index("phase4_users")
        social_idx = resolved.index("phase6_social")

        assert cities_idx < users_idx
        assert users_idx < social_idx

    def test_phase6_independent_of_reviews(self):
        """Test that Phase 6 doesn't require Phase 5 in dependency chain."""
        from generators.phase1_definitions import CitiesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        # Register minimal chain: Cities -> Users -> Social
        registry.register(CitiesPhase())
        registry.register(UsersPhase())
        registry.register(SocialGraphPhase())

        # Phase 6 should NOT require Phase 5 (reviews)
        resolved = registry.resolve_dependencies(["phase6_social"])

        # Should only have 3 phases
        assert len(resolved) == 3
        assert "phase5_reviews" not in resolved
        assert "phase3_dishes" not in resolved
        assert "phase2_restaurants" not in resolved

        # Only Cities -> Users -> Social
        assert resolved == ["phase1_cities", "phase4_users", "phase6_social"]
