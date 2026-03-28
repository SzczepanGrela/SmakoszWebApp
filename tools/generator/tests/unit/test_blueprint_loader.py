import pytest

from utils.blueprint_db import BlueprintDB


class TestDishesBlueprint:

    def test_dishes_json_loads(self, dishes_json):
        assert dishes_json is not None
        assert len(dishes_json) > 0, "dishes.json should have archetypes"

    def test_all_archetypes_have_required_fields(self, dishes_json):
        required_fields = ["base_price", "archetype_base", "variants"]

        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            for field in required_fields:
                assert field in archetype_data, f"Archetype '{archetype_name}' missing required field '{field}'"

    def test_archetype_base_has_characteristics(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            base = archetype_data.get("archetype_base", {})
            assert "characteristics" in base, f"Archetype '{archetype_name}' missing archetype_base.characteristics"
            assert "default_weights" in base, f"Archetype '{archetype_name}' missing archetype_base.default_weights"

    def test_all_variants_have_ingredients(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            for variant_name, variant_data in archetype_data.get("variants", {}).items():
                assert "ingredients" in variant_data, f"Variant '{archetype_name}.{variant_name}' missing ingredients"
                assert isinstance(variant_data["ingredients"], list), (
                    f"Variant '{archetype_name}.{variant_name}' ingredients must be list"
                )

    def test_variant_price_multiplier_reasonable(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            for variant_name, variant_data in archetype_data.get("variants", {}).items():
                multiplier = variant_data.get("price_multiplier", {})
                if isinstance(multiplier, dict):
                    mean = multiplier.get("mean", 1.0)
                    assert 0.1 <= mean <= 5.0, (
                        f"Variant '{archetype_name}.{variant_name}' price multiplier {mean} out of range"
                    )

    def test_characteristics_values_normalized(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            base_chars = archetype_data.get("archetype_base", {}).get("characteristics", {})
            for dim, val in base_chars.items():
                assert 0.0 <= val <= 1.0, f"Archetype '{archetype_name}' characteristic {dim}={val} out of [0,1]"

            for variant_name, variant_data in archetype_data.get("variants", {}).items():
                var_chars = variant_data.get("characteristics", {})
                for dim, val in var_chars.items():
                    assert 0.0 <= val <= 1.0, f"Variant '{archetype_name}.{variant_name}' {dim}={val} out of [0,1]"


class TestBlueprintDB:

    def test_archetypes_loaded(self):
        bdb = BlueprintDB()
        names = bdb.get_archetype_names()
        assert len(names) == 46
        assert "Pizza" in names
        bdb.close()

    def test_variants_have_characteristics(self):
        bdb = BlueprintDB()
        variants = bdb.get_all_variants_with_details()
        assert len(variants) > 0
        for v in variants:
            assert v["characteristics"], f"Variant '{v['name']}' has empty characteristics"
        bdb.close()

    def test_variant_price_multiplier_reasonable(self):
        bdb = BlueprintDB()
        variants = bdb.get_all_variants_with_details()
        for v in variants:
            assert 0.1 <= v["price_multiplier_mean"] <= 5.0, (
                f"Variant '{v['name']}' price multiplier {v['price_multiplier_mean']} out of range"
            )
        bdb.close()

    def test_ingredients_have_names(self):
        bdb = BlueprintDB()
        ingredients = bdb.get_all_ingredients()
        assert len(ingredients) == 286
        for ing in ingredients:
            assert ing["name"], "Ingredient has empty name"
        bdb.close()

    def test_all_variants_have_ingredients(self):
        bdb = BlueprintDB()
        variants = bdb.get_all_variants_with_details()
        empty = []
        for v in variants:
            ings = bdb.get_variant_ingredients(v["id"])
            if not ings:
                empty.append(f"{v['archetype_name']}/{v['name']}")
        bdb.close()
        if empty:
            pytest.skip(f"{len(empty)} variants without ingredients")

    def test_themes_have_sections(self):
        bdb = BlueprintDB()
        themes = bdb.get_themes()
        assert len(themes) == 26
        for t in themes:
            sections = bdb.get_theme_sections(t["name"])
            assert sections, f"Theme '{t['name']}' has no sections"
        bdb.close()

    def test_all_theme_archetypes_have_section_routes(self):
        bdb = BlueprintDB()
        themes = bdb.get_themes()
        missing = []
        for t in themes:
            for arch in bdb.get_theme_archetypes(t["name"]):
                secs = bdb.get_sections_for_dish(t["name"], arch)
                if not secs:
                    missing.append(f"{t['name']} -> {arch}")
        bdb.close()
        assert not missing, f"Missing section routes: {missing}"

    def test_characteristics_values_normalized(self):
        bdb = BlueprintDB()
        variants = bdb.get_all_variants_with_details()
        violations = []
        for v in variants:
            for dim, val in v["characteristics"].items():
                if not (0.0 <= val <= 1.0):
                    violations.append(f"{v['archetype_name']}/{v['name']} {dim}={val}")
        bdb.close()
        assert not violations, f"Characteristic values out of [0,1]: {violations[:5]}"

    def test_ingredient_dietary_flags(self):
        bdb = BlueprintDB()
        flags = bdb.get_ingredient_dietary_flags(["kurczak", "mozzarella", "pomidor"])
        bdb.close()
        assert flags["kurczak"]["is_meat"] is True
        assert flags["mozzarella"]["is_dairy"] is True
        assert flags["pomidor"]["is_meat"] is False
