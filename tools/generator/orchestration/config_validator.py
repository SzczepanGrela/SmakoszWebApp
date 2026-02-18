"""
Configuration Validation with Pydantic

Provides type-safe validation for GENERATION_CONFIG:
- Type checking (int, float, bool)
- Range validation (gt, ge, le constraints)
- Cross-field validation (proportionality rules)
- Clear error messages for invalid configs
"""

from pydantic import BaseModel, ConfigDict, Field, field_validator

class GenerationConfigSchema(BaseModel):
    """
    Validated generation configuration using Pydantic.

    Benefits:
    - Type safety across codebase
    - Runtime validation with clear errors
    - IDE autocomplete
    - Self-documenting structure
    """

    # Core entity counts
    num_users: int = Field(
        gt=0, le=1_000_000, description="Number of users to generate"
    )
    num_restaurants: int = Field(
        gt=0, le=100_000, description="Number of restaurants to generate"
    )
    num_dishes: int = Field(
        gt=0, le=1_000_000, description="Number of dishes to generate"
    )

    # Review generation
    avg_reviews_per_user: int = Field(
        gt=0, le=1000, description="Average reviews per user"
    )
    power_user_percentage: float = Field(
        ge=0.0,
        le=1.0,
        description="Percentage of users that are power users (0.0-1.0)",
    )

    # Distribution parameters
    zipf_alpha: float = Field(
        gt=0.0, le=3.0, description="Zipf distribution alpha parameter"
    )
    default_mood_propensity: float = Field(
        ge=0.0, le=1.0, description="Default mood propensity (0.0-1.0)"
    )

    # Performance settings
    worker_cpu_usage_percent: float = Field(
        ge=0.1, le=1.0, description="CPU usage for multiprocessing (0.1-1.0)"
    )
    max_db_connections_limit: int = Field(
        ge=1, le=100, description="Max database connections for workers"
    )

    # Percentages (0-100)
    premium_restaurant_pct: int = Field(
        ge=0, le=100, description="Percentage of premium restaurants"
    )
    budget_restaurant_pct: int = Field(
        ge=0, le=100, description="Percentage of budget restaurants"
    )

    # Boolean flags
    enable_dietary_restrictions: bool = Field(
        default=True, description="Enable dietary restriction tracking"
    )
    enable_allergen_tracking: bool = Field(
        default=True, description="Enable allergen tracking"
    )

    # Pydantic v2 configuration
    model_config = ConfigDict(
        extra="forbid",  # Reject unknown fields (catch typos)
        use_enum_values=True,  # Use enum values instead of enum objects
    )

    @field_validator("num_dishes")
    @classmethod
    def dishes_proportional_to_restaurants(cls, v, info):
        """
        Validate dishes are proportional to restaurants.

        Rule: 5-20 dishes per restaurant on average
        """
        # In Pydantic v2, use info.data to access other fields
        if "num_restaurants" in info.data:
            num_restaurants = info.data["num_restaurants"]
            min_dishes = num_restaurants * 5
            max_dishes = num_restaurants * 20
            if not (min_dishes <= v <= max_dishes):
                raise ValueError(
                    f"num_dishes ({v:,}) should be 5-20x num_restaurants "
                    f"({num_restaurants:,}). "
                    f"Expected range: {min_dishes:,} - {max_dishes:,}"
                )
        return v

    @field_validator("budget_restaurant_pct")
    @classmethod
    def premium_plus_budget_not_exceed_100(cls, v, info):
        """Validate premium + budget percentages don't exceed 100%."""
        # Check if premium_restaurant_pct has been validated already
        if "premium_restaurant_pct" in info.data:
            premium = info.data["premium_restaurant_pct"]
            total = premium + v
            if total > 100:
                raise ValueError(
                    f"premium_restaurant_pct ({premium}) + budget_restaurant_pct "
                    f"({v}) = {total}% exceeds 100%"
                )
        return v

    @field_validator("num_users")
    @classmethod
    def users_reasonable_for_reviews(cls, v):
        """Validate user count can support review generation."""
        # With default avg_reviews_per_user=40, we'll generate ~40*num_users reviews
        # This should be feasible to distribute across restaurants
        if v < 100:
            raise ValueError(
                f"num_users ({v}) is too low. Minimum 100 users recommended "
                f"for realistic data distribution."
            )
        return v

def validate_config(config: dict) -> GenerationConfigSchema:
    """
    Validate generation config and return typed schema.

    Args:
        config: Configuration dictionary from GENERATION_CONFIG

    Returns:
        Validated GenerationConfigSchema instance

    Raises:
        ValidationError: If config is invalid (with detailed error messages)

    Example:
        >>> from config import GENERATION_CONFIG
        >>> validated = validate_config(GENERATION_CONFIG)
        >>> print(validated.num_users)  # Type-safe!
        50000
    """
    return GenerationConfigSchema(**config)

def get_config_summary(config: GenerationConfigSchema) -> str:
    """
    Get human-readable summary of configuration.

    Args:
        config: Validated configuration

    Returns:
        Formatted summary string
    """
    return f"""
Configuration Summary:
  Users:        {config.num_users:,}
  Restaurants:  {config.num_restaurants:,}
  Dishes:       {config.num_dishes:,}
  Reviews/user: {config.avg_reviews_per_user}
  Power users:  {config.power_user_percentage * 100:.1f}%
  Workers:      CPU {config.worker_cpu_usage_percent * 100:.0f}%, Max Connections {config.max_db_connections_limit}
    """.strip()
